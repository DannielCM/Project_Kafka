using BackendAuthentication;
using Microsoft.Extensions.Configuration;
using MyAuthenticationBackend.Models;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Security.Cryptography;
using OtpNet;
using QRCoder;

namespace MyAuthenticationBackend.Services;
public class AuthenticationServices
{
    private readonly DbHelper _dbHelper;
    private readonly IConfiguration _config;
    private readonly JwtServices _jwtService;

    public AuthenticationServices(DbHelper dbHelper, IConfiguration config, JwtServices jwtService)
    {
        _dbHelper = dbHelper;
        _config = config;
        _jwtService = jwtService;
    }


    public async Task<LoginResult> AuthenticateUser(string email, string password)
    {
        email = email?.Trim() ?? "";
        password = password?.Trim() ?? "";

        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            validationErrors.Add("EMAIL AND PASSWORD CANNOT BE EMPTY");

        if (!email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            validationErrors.Add("ONLY GMAIL ADDRESSES ARE ALLOWED");

        if (validationErrors.Count > 0)
        {
            return new LoginResult
            {
                Success = false,
                Message = string.Join("; ", validationErrors)
            };
        }

        await using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        Account? account = null;

        const string selectQuery = "SELECT * FROM accounts WHERE email = @Email";
        await using (var cmd = new MySqlCommand(selectQuery, conn))
        {
            cmd.Parameters.AddWithValue("@Email", email);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                account = new Account
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    StoredHashedPassword = reader.GetString(reader.GetOrdinal("password")),
                    Role = reader.GetString(reader.GetOrdinal("role")),
                    TwoFactorSecret = reader.IsDBNull(reader.GetOrdinal("two_factor_secret")) ? null : reader.GetString(reader.GetOrdinal("two_factor_secret"))
                };
            }
        }

        if (!BCrypt.Net.BCrypt.Verify(password, account?.StoredHashedPassword ?? _config["Auth:DummyHash"]))
        {
            return new LoginResult
            {
                Success = false,
                Message = "INVALID CREDENTIALS"
            };
        }

        // Handle two-factor authentication
        if (account?.TwoFactorSecret != null)
        {
            var tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            var token = Convert.ToBase64String(tokenBytes);
            var expiresAt = DateTime.UtcNow.AddMinutes(5);

            const string insertQuery = @"
                INSERT INTO tfa_login_request (token, account_id, expires_at, used)
                VALUES (@Token, @AccountId, @ExpiresAt, 0)";
            await using (var insertCmd = new MySqlCommand(insertQuery, conn))
            {
                insertCmd.Parameters.AddWithValue("@Token", token);
                insertCmd.Parameters.AddWithValue("@AccountId", account.Id);
                insertCmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

                await insertCmd.ExecuteNonQueryAsync();
            }

            var baseRedirectUrl = _config["Auth:TwoFactorRedirectUrl"] ?? "/verify-2fa";
            var redirectUrl = $"{baseRedirectUrl}?token={Uri.EscapeDataString(token)}";

            return new LoginResult
            {
                Success = true,
                RedirectUrl = redirectUrl,
                Message = "2FA REQUIRED"
            };
        }

        // Generate JWT token
        var finalToken = _jwtService.GenerateToken(account!.Id, account.Email, account.Role, 60);

        const string updateQuery = "UPDATE accounts SET last_login = NOW() WHERE id = @Id";
        await using (var updateCmd = new MySqlCommand(updateQuery, conn))
        {
            updateCmd.Parameters.AddWithValue("@Id", account.Id);
            await updateCmd.ExecuteNonQueryAsync();
        }

        return new LoginResult
        {
            Success = true,
            Token = finalToken,
            Message = "LOGIN SUCCESSFUL",
            Account = new Account
            {
                Id = account.Id,
                Email = account.Email
            }
        };
    }

    public async Task<RegisterResults> RegisterUser(RegisterRequest request)
    {
        // Input sanitization
        request.Email = (request.Email ?? "").Trim();
        request.Password = (request.Password ?? "").Trim();
        request.FirstName = (request.FirstName ?? "").Trim();
        request.MiddleName = request.MiddleName?.Trim() ?? string.Empty;
        request.LastName = (request.LastName ?? "").Trim();
        request.Role = (request.Role ?? "").Trim();

        var validationErrors = new List<string>();

        // Validation
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            validationErrors.Add("EMAIL CANNOT BE EMPTY");
        }
        else if (!request.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            validationErrors.Add("ONLY GMAIL ADDRESSES ARE ALLOWED");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            validationErrors.Add("PASSWORD CANNOT BE EMPTY");
        }
        else if (request.Password.Length < 5)
        {
            validationErrors.Add("PASSWORD MUST BE AT LEAST 5 CHARACTERS LONG");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            validationErrors.Add("FIRST NAME CANNOT BE EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            validationErrors.Add("LAST NAME CANNOT BE EMPTY");
        }

        if (validationErrors.Count > 0)
        {
            return new RegisterResults
            {
                Success = false,
                Message = string.Join("; ", validationErrors)
            };
        }

        await using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            // Check if email exists
            await using (var checkCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM accounts WHERE email = @Email", conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("@Email", request.Email);
                var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (existingCount > 0)
                {
                    await transaction.RollbackAsync();
                    return new RegisterResults
                    {
                        Success = false,
                        Message = "EMAIL ALREADY IN USE"
                    };
                }
            }

            // Hash password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Insert into accounts
            await using (var cmd = new MySqlCommand(@"
                INSERT INTO accounts(email, password, role)
                VALUES(@Email, @Password, @Role);
            ", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Email", request.Email);
                cmd.Parameters.AddWithValue("@Password", hashedPassword);
                cmd.Parameters.AddWithValue("@Role", request.Role ?? "basic");

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    throw new Exception("Failed to create account!");
            }

            // Insert into users (using LAST_INSERT_ID)
            await using (var cmd2 = new MySqlCommand(@"
                INSERT INTO users(account_id, first_name, middle_name, last_name)
                VALUES(LAST_INSERT_ID(), @FirstName, @MiddleName, @LastName);
            ", conn, transaction))
            {
                cmd2.Parameters.AddWithValue("@FirstName", request.FirstName);
                cmd2.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(request.MiddleName) ? DBNull.Value : request.MiddleName );
                cmd2.Parameters.AddWithValue("@LastName", request.LastName);

                await cmd2.ExecuteNonQueryAsync();
            }

            // Commit if all succeed
            await transaction.CommitAsync();

            return new RegisterResults
            {
                Success = true,
                Message = "ACCOUNT CREATED SUCCESSFULLY"
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return new RegisterResults
            {
                Success = false,
                Message = $"REGISTRATION FAILED: {ex.Message}"
            };
        }
    }

    public async Task<TFAVerificationResult> VerifyTFA(String? token, String? code)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TFAVerificationResult { IsSuccess = false, Message = "Token cannot be empty!" };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new TFAVerificationResult { IsSuccess = false, Message = "TFA pin cannot be empty!" };
        }

        using var connection = _dbHelper.GetConnection();
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();

        TFALogInRequest? tfaLogInRequest = null;
        try
        {
            using (var cmd = new MySqlCommand(@"
                SELECT * FROM tfa_login_request WHERE token = @Token
            ", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Token", token);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    tfaLogInRequest = new TFALogInRequest
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Token = reader.GetString(reader.GetOrdinal("token")),
                        AccountId = reader.GetInt32(reader.GetOrdinal("account_id")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        ExpiresAt = reader.GetDateTime(reader.GetOrdinal("expires_at")),
                        Used = reader.GetBoolean(reader.GetOrdinal("used"))
                    };
                }
            }

            if (tfaLogInRequest == null)
            {
                return new TFAVerificationResult { IsSuccess = false, Message = "Invalid or expired 2FA token." };
            }

            if (tfaLogInRequest.Used)
            {
                return new TFAVerificationResult { IsSuccess = false, Message = "This 2FA token has already been used." };
            }

            if (tfaLogInRequest.ExpiresAt < DateTime.UtcNow)
            {
                return new TFAVerificationResult { IsSuccess = false, Message = "This 2FA token has expired." };
            }

            Account? account = null;
            using (var cmd = new MySqlCommand(@"
                SELECT * FROM accounts 
                WHERE id = @Id;
            ", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", tfaLogInRequest.AccountId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    account = new Account
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Email = reader.GetString(reader.GetOrdinal("email")),
                        StoredHashedPassword = reader.GetString(reader.GetOrdinal("password")),
                        Role = reader.GetString(reader.GetOrdinal("role")),
                        TwoFactorSecret = reader.IsDBNull(reader.GetOrdinal("two_factor_secret")) ? null : reader.GetString(reader.GetOrdinal("two_factor_secret"))
                    };
                }
            }

            if (account == null)
            {
                return new TFAVerificationResult { IsSuccess = false, Message = "Account could not be verified!" };
            }

            if (string.IsNullOrEmpty(account.TwoFactorSecret))
            {
                return new TFAVerificationResult { IsSuccess = false, Message = "2FA is not set up for this account." };
            }

            var totp = new Totp(Base32Encoding.ToBytes(account.TwoFactorSecret));
            bool isValid = totp.VerifyTotp(code, out long timeStepMatched, VerificationWindow.RfcSpecifiedNetworkDelay);
            if (!isValid)
            {
                return new TFAVerificationResult { IsSuccess = false, Message = "Invalid 2FA code." };
            }

            // Mark the token as used and expired
            using (var updateCmd = new MySqlCommand(@"
                UPDATE tfa_login_request
                SET used = 1, expires_at = @now
                WHERE id = @Id
            ", connection, transaction))
            {
                updateCmd.Parameters.AddWithValue("@Id", tfaLogInRequest.Id);
                updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

                await updateCmd.ExecuteNonQueryAsync();
            }

            // Update last login timestamp for the account
            using (var updateLoginCmd = new MySqlCommand("UPDATE accounts SET last_login = NOW() WHERE id = @Id", connection, transaction))
            {
                updateLoginCmd.Parameters.AddWithValue("@Id", account.Id);
                await updateLoginCmd.ExecuteNonQueryAsync();
            }

            return new TFAVerificationResult { IsSuccess = true, Account = account, Message = "Verified!" };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return new TFAVerificationResult { IsSuccess = false, Message = $"2FA verification failed: {ex.Message}" };
        }
    }

    public async Task<EnableTFAResult> EnableTFA(int enabled = 1, String? email = "", int userId = 0)
    {
        Console.WriteLine($"EnableTFA called with enabled={enabled}, email={email}, userId={userId}");
        if (enabled > 1 || enabled < 0)
        {
            throw new Exception("Enabled must be 0 or 1.");
        }

        if (string.IsNullOrWhiteSpace(email) || userId <= 0)
        {
            return new EnableTFAResult
            {
                Success = false,
                Message = "Invalid email or user ID."
            };
        }

        using var connection = _dbHelper.GetConnection();
        await connection.OpenAsync();

        // Check if the account already has 2FA enabled
        using (var checkCmd = new MySqlCommand("SELECT two_factor_secret FROM accounts WHERE id = @id", connection))
        {
            checkCmd.Parameters.AddWithValue("@id", userId);
            var existingSecret = await checkCmd.ExecuteScalarAsync();

            if (existingSecret != DBNull.Value && existingSecret != null && enabled == 1)
            {
                return new EnableTFAResult { Success = false, Message = "2FA is already enabled." };
            }
        }

        bool enable2FA = enabled == 1;
        string base32Secret = "";
        string qrCodeBase64 = "";

        if (enable2FA)
        {
            var secretKey = KeyGeneration.GenerateRandomKey(20);
            base32Secret = Base32Encoding.ToString(secretKey);

            string issuer = _config["Jwt:Issuer"] ?? "MyBackend";
            string otpauthUri = $"otpauth://totp/{issuer}:{email}?secret={base32Secret}&issuer={issuer}&digits=6";

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);

            byte[] qrCodeImage = qrCode.GetGraphic(20);
            qrCodeBase64 = Convert.ToBase64String(qrCodeImage);
        }

        using var cmd = new MySqlCommand(@"
            UPDATE accounts 
            SET two_factor_secret = @secret 
            WHERE id = @id;
        ", connection);
        cmd.Parameters.AddWithValue("@secret", enable2FA ? base32Secret : DBNull.Value);
        cmd.Parameters.AddWithValue("@id", userId);
        await cmd.ExecuteNonQueryAsync();

        return new EnableTFAResult { Success = true, Message = "2FA setup updated.", QRCodeImageBase64 = qrCodeBase64, ManualEntryKey = base32Secret };
    }
}
