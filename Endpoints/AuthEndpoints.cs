using MySql.Data.MySqlClient;
using BCrypt.Net;
using BackendAuthentication;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using MyAuthenticationBackend.Services;
using CaptchaGen.NetCore;
using OtpNet;
using QRCoder;
using MyAuthenticationBackend.Models;

namespace AuthenticationBackend.Endpoints;
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/auth");

        group.MapGet("/", () => "authentication route");

        group.MapPost("/login", async (IMemoryCache cache, KafkaProducerService kafkaProducerService, AuthenticationServices authService, LogInRequest request, CaptchaServices captchaServices) =>
        {
            var email = request.Email.Trim().ToLower();
            var password = request.Password.Trim();
            var captchaId = request.CaptchaId.Trim();
            var captchaText = request.CaptchaText.Trim();

            try
            {
                var captcha_result = captchaServices.ValidateCaptcha(captchaId, captchaText);
                if (captcha_result.Success == false)
                {
                    return Results.BadRequest(new { message = captcha_result.Message ?? "CAPTCHA VALIDATION FAILED!" });
                }

                var result = await authService.AuthenticateUser(email, password);
                if (!result.Success)
                {
                    return Results.BadRequest(new { message = result.Message ?? "LOGIN FAILED!" });
                }

                if (!string.IsNullOrEmpty(result.RedirectUrl))
                {
                    // 2FA required
                    return Results.Json(new { message = result.Message ?? "2FA REQUIRED", redirectUrl = result.RedirectUrl }, statusCode: 200);
                }

                cache.Remove(captchaId);

                kafkaProducerService.SendLoginMessage($"{ result.Account.Id}");

                return Results.Json(new { message = "LOGIN SUCCESSFUL!", token = result.Token }, statusCode: 200);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Results.Json(new { message = "Internal server error." }, statusCode: 500);
            }
        });

        group.MapPost("/register", async (DbHelper dbHelper, AuthenticationServices authService, RegisterRequest request) =>
        {
            try
            {
                request.Email = request.Email!.Trim().ToLower();
                request.Password = request.Password!.Trim();

                var result = await authService.RegisterUser(request);

                if (!result.Success)
                {
                    return Results.BadRequest(new { message = result.Message ?? "REGISTER FAILED!" });
                }

                return Results.Json(new { message = "REGISTER SUCCESSFUL!" }, statusCode: 201);
            } 
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Results.Json(new { message = "INTERNAL SERVER ERROR!" }, statusCode: 500);
            }
        });

        group.MapPost("/change-password", [Authorize] async (DbHelper dbHelper, HttpContext httpContext, UserServices userServices, ResetPasswordRequest request) =>
        {
            var currentPassword = string.IsNullOrWhiteSpace(request.CurrentPassword) ? "" : request.CurrentPassword.Trim();
            var newPassword = string.IsNullOrWhiteSpace(request.NewPassword) ? "" : request.NewPassword.Trim();
            var newPasswordConfirmation = string.IsNullOrWhiteSpace(request.NewPasswordConfirmation) ? "" : request.NewPasswordConfirmation.Trim();

            try
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(userIdClaim, out int userId);

                var result = await userServices.ChangePassword(userId, currentPassword, newPassword, newPasswordConfirmation);

                if (!result.Success)
                {
                    return Results.BadRequest(new { message = result.Message });
                }

                return Results.Ok(new { message = "PASSWORD RESET SUCCESSFUL!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Results.Json(new { message = "Internal Server error." }, statusCode: 500);
            }
        });

        // move the logic to a service class later, since the handler / endpoint is too cluttered.
        group.MapGet("/captcha/generate", (IMemoryCache cache) =>
        {
            // generate random code
            string code = ImageFactory.CreateCode(5);

            // create memory buffer for holding image data, 
            using var ms = new MemoryStream();
            // generate image and save to memory buffer
            using (var img = ImageFactory.BuildImage(code, 60, 200, 20, 10))
            {
                img.CopyTo(ms);
            }

            // generate unique catpcha id
            string captchaId = Guid.NewGuid().ToString();
            // store the captcha code with id in the cache for 5 minutes
            cache.Set(captchaId, code, TimeSpan.FromMinutes(5));

            return Results.File(ms.ToArray(), "image/jpeg", captchaId);
        });

        // move the logic to a service class later, since the handler / endpoint is too cluttered.
        group.MapGet("/2fa", [Authorize] async (HttpContext http, DbHelper dbHelper) =>
        {
            // Get user ID from NameIdentifier claim
            var userIdClaim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
            {
                return Results.Unauthorized();
            }
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Results.BadRequest("Invalid user ID.");
            }

            var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            var cmd = new MySqlCommand(@"
                SELECT two_factor_secret 
                FROM accounts 
                WHERE id = @id;
            ", connection);
            cmd.Parameters.AddWithValue("@id", userId);

            var result = await cmd.ExecuteScalarAsync();
            bool twoFactorEnabled = result != null && result != DBNull.Value;

            // Return whether 2FA is enabled
            return Results.Json(new { enabled = twoFactorEnabled }, statusCode: 200);
        });

        // move the logic to a service class later, since the handler / endpoint is too cluttered.
        group.MapPost("/2fa/setup", [Authorize] async (IConfiguration config, DbHelper dbHelper, HttpContext httpContext, TFASetupRequest request) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Results.BadRequest("Invalid user ID.");
            }

            bool enable2FA = request.Enabled == 1;
            string base32Secret = "";
            string qrCodeBase64 = "";

            if (enable2FA)
            {
                // Generate a new OTP secret key for the user
                var secretKey = KeyGeneration.GenerateRandomKey(20);
                base32Secret = Base32Encoding.ToString(secretKey);

                // Generate the otpauth URI
                string issuer = config["Jwt:Issuer"] ?? "MyBackend";
                string otpauthUri = $"otpauth://totp/{issuer}:{email}?secret={base32Secret}&issuer={issuer}&digits=6";

                // Generate QR code
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);

                byte[] qrCodeImage = qrCode.GetGraphic(20);
                qrCodeBase64 = Convert.ToBase64String(qrCodeImage);
            }

            // Update the database
            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            using var cmd = new MySqlCommand(@"
                UPDATE accounts 
                SET two_factor_secret = @secret 
                WHERE id = @id;
            ", connection);
            cmd.Parameters.AddWithValue("@secret", enable2FA ? base32Secret : DBNull.Value);
            cmd.Parameters.AddWithValue("@id", userId);
            await cmd.ExecuteNonQueryAsync();

            return Results.Json(new
            {
                enabled = enable2FA,
                secret = base32Secret,
                qrCodeImage = qrCodeBase64
            });
        });

        // move the logic to a service class later, since the handler / endpoint is too cluttered.
        group.MapPost("/2fa/verify", async (DbHelper dbHelper, HttpContext httpContext, KafkaProducerService kafkaProducerService, JwtServices jwtService, TFAVerificationRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.Json(new { message = "Unauthorized access!" }, statusCode: 403);
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.BadRequest(new { message = "2FA code is required." });
            }

            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            TFALogInRequest? tfaLogInRequest = null;
            using (var cmd = new MySqlCommand(@"
                SELECT * FROM tfa_login_request WHERE token = @Token
            ", connection))
            {
                cmd.Parameters.AddWithValue("@Token", request.Token);

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
                return Results.BadRequest(new { message = "Invalid or expired 2FA token." });
            }

            if (tfaLogInRequest.Used)
            {
                return Results.BadRequest(new { message = "This 2FA token has already been used." });
            }

            if (tfaLogInRequest.ExpiresAt < DateTime.UtcNow)
            {
                return Results.BadRequest(new { message = "This 2FA token has expired." });
            }

            Account? account = null;
            using (var cmd = new MySqlCommand(@"
                SELECT * FROM accounts 
                WHERE id = @Id;
            ", connection))
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
                return Results.BadRequest(new { message = "Account not found." });
            }

            if (string.IsNullOrEmpty(account.TwoFactorSecret))
            {
                return Results.BadRequest(new { message = $"2FA is not set up for this account." });
            }

            var totp = new Totp(Base32Encoding.ToBytes(account.TwoFactorSecret));
            bool isValid = totp.VerifyTotp(request.Code, out long timeStepMatched, VerificationWindow.RfcSpecifiedNetworkDelay);
            if (!isValid)
            {
                return Results.BadRequest(new { message = "Invalid 2FA code." });
            }

            // Mark the token as used and expired
            using (var updateCmd = new MySqlCommand(@"
                UPDATE tfa_login_request
                SET used = 1, expires_at = @now
                WHERE id = @Id
            ", connection))
            {
                updateCmd.Parameters.AddWithValue("@Id", tfaLogInRequest.Id);
                updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                await updateCmd.ExecuteNonQueryAsync();
            }

            var token = jwtService.GenerateToken(account.Id, account.Email, account.Role, 60);

            // Update last login timestamp for the account
            using (var updateLoginCmd = new MySqlCommand("UPDATE accounts SET last_login = NOW() WHERE id = @Id", connection))
            {
                updateLoginCmd.Parameters.AddWithValue("@Id", account.Id);
                await updateLoginCmd.ExecuteNonQueryAsync();
            }

            kafkaProducerService.SendLoginMessage($"{account.Id}");

            return Results.Json(new { message = "2FA VERIFICATION SUCCESSFUL!", token = token }, statusCode: 200);
        });
    }
}

