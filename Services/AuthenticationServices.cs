using BackendAuthentication;
using Microsoft.Extensions.Configuration;
using MyAuthenticationBackend.Models;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using System.Security.Cryptography;

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
        {
            validationErrors.Add("EMAIL AND PASSWORD CANNOT BE EMPTY");
        }
        if (!email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            validationErrors.Add("ONLY GMAIL ADDRESSES ARE ALLOWED");
        }

        if (validationErrors.Count > 0)
        {
            return new LoginResult
            {
                Success = false,
                Message = string.Join("; ", validationErrors)
            };
        }

        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        using var cmd = new MySqlCommand("SELECT * FROM accounts WHERE email = @email", conn);
        cmd.Parameters.AddWithValue("@email", email);

        Account? account = null;
        using (var reader = await cmd.ExecuteReaderAsync()) {
            if (await reader.ReadAsync())
            {
                account = new Account
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    StoredHashedPassword = reader.GetString(reader.GetOrdinal("password")),
                    Role = reader.GetString(reader.GetOrdinal("role")),
                    TwoFactorSecret = reader.IsDBNull(reader.GetOrdinal("two_factor_secret"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("two_factor_secret"))
                };
            }
        }

        if (!BCrypt.Net.BCrypt.Verify(password, account?.StoredHashedPassword ?? _config["Auth.DummyHash"]))
        {
            return new LoginResult { Success = false, Message = "INVALID CREDENTIALS" };
        }

        if (account.TwoFactorSecret != null)
        {
            var tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            var token = Convert.ToBase64String(tokenBytes);

            var expiresAt = DateTime.UtcNow.AddMinutes(5);

            using (var insertCmd = new MySqlCommand(@"
                INSERT INTO tfa_login_request (token, account_id, expires_at, used)
                VALUES (@token, @AccountId, @expiresAt, 0)
            ", conn))
            {
                insertCmd.Parameters.AddWithValue("@token", token);
                insertCmd.Parameters.AddWithValue("@AccountId", account.Id);
                insertCmd.Parameters.AddWithValue("@expiresAt", expiresAt);

                await insertCmd.ExecuteNonQueryAsync();
            }

            // escape sequence the token before sending it in the URL
            var baseRedirectUrl = _config["Auth.TwoFactorRedirectUrl"] ?? "/verify-2fa";
            var redirectUrl = $"{baseRedirectUrl}?token={Uri.EscapeDataString(token)}";

            return new LoginResult
            {
                Success = true,
                RedirectUrl = redirectUrl,
                Message = "2FA REQUIRED"
            };
        }

        var finalToken = _jwtService.GenerateToken(account.Id, account.Email, account.Role, 5);

        using var updateCmd = new MySqlCommand("UPDATE accounts SET last_login = NOW() WHERE id = @Id", conn);
        updateCmd.Parameters.AddWithValue("@Id", account.Id);
        await updateCmd.ExecuteNonQueryAsync();

        return new LoginResult
        {
            Success = true,
            Token = finalToken,
            Message = "LOGIN SUCCESSFUL",
            Account = new Account { Id = account.Id, Email = account.Email }
        };
    }

    public async Task<RegisterResults> RegisterUser(RegisterRequest request)
    {
        var validationErrors = new List<string>();

        if (string.IsNullOrEmpty(request.Email))
        {
            validationErrors.Add("EMAIL CANNOT BE EMPTY");
        }

        if (!string.IsNullOrEmpty(request.Email) && !request.Email.EndsWith("@gmail.com"))
        {
            validationErrors.Add("ONLY GMAIL ADDRESSES ARE ALLOWED");
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            validationErrors.Add("PASSWORD CANNOT BE EMPTY");
        }
        else if (request.Password.Length < 5)
        {
            validationErrors.Add("PASSWORD MUST BE AT LEAST 5 CHARACTERS LONG");
        }

        if (validationErrors.Count > 0)
        {
            return new RegisterResults
            {
                Success = false,
                Message = string.Join("; ", validationErrors)
            };
        }

        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        using var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM accounts WHERE email = @Email", conn);
        checkCmd.Parameters.AddWithValue("@Email", request.Email);
        var existingCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
        if (existingCount > 0)
        {
            return new RegisterResults
            {
                Success = false,
                Message = "EMAIL ALREADY IN USE"
            };
        }

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        using var cmd = new MySqlCommand(@"
                    INSERT INTO accounts(email, password, role)
                    VALUES(@email, @password, @role)
                    ", conn);
        cmd.Parameters.AddWithValue("@email", request.Email);
        cmd.Parameters.AddWithValue("@password", hashedPassword);
        cmd.Parameters.AddWithValue("@role", request.Role ?? "basic");

        int rowsAffected = await cmd.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            throw new Exception("Failed to create account!");
        }

        return new RegisterResults { 
            Success = true, 
            Message = "ACCOUNT CREATED SUCCESSFULLY"
        };
    }
}
