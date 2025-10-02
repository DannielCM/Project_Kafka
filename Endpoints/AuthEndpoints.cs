using MySql.Data.MySqlClient;
using BCrypt.Net;
using BackendAuthentication;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using MyAuthenticationBackend.AppServices;
using CaptchaGen.NetCore;
using OtpNet;
using QRCoder;
using MyAuthenticationBackend.Models;

namespace AuthenticationBackend.Endpoints;
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/", () => "authentication route");

        group.MapPost("/login", async (IMemoryCache cache, AuthenticationServices authService, LogInRequest request) =>
        {
            var email = request.Email!.Trim().ToLower();
            var password = request.Password!.Trim();
            var captchaId = request.CaptchaId!.Trim();
            var captchaText = request.CaptchaText!.Trim();

            try
            {
                // handle captcha validation ?? Make it modular perhaps? If I have the time to.
                if (!cache.TryGetValue(captchaId, out string? storedText) || storedText == null || !storedText.Equals(captchaText, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { success = false, message = "CAPTCHA incorrect" });
                }
                cache.Remove(captchaId);

                var result = await authService.AuthenticateUser(request);

                if (!result.Success)
                {
                    return Results.BadRequest(new { message = result.Message ?? "LOGIN FAILED!" });
                }

                // disable kafka service for now as it may cause problems.
                // kafka service
                //kafkaService.SendLoginMessage(id);

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
                request.Role = request.Role!.Trim().ToLower();

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

        group.MapPost("/change-password", [Authorize(Policy = "Require2FAVerified")] async (DbHelper dbHelper, HttpContext httpContext, UserServices userServices, ResetPasswordRequest request) =>
        {
            var currentPassword = request.CurrentPassword!.Trim();
            var newPassword = request.NewPassword!.Trim();
            var newPasswordConfirmation = request.NewPasswordConfirmation!.Trim();

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
        group.MapGet("/2fa/setup", [Authorize(Policy = "Require2FAVerified")] async (IConfiguration config, DbHelper dbHelper, HttpContext httpContext) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdClaim, out int userId);

            // Generate a new secret key for the user
            var secretKey = KeyGeneration.GenerateRandomKey(20);
            var base32Secret = Base32Encoding.ToString(secretKey);

            // Generate the otpauth URI
            string issuer = config["Jwt:Issuer"] ?? "MyBackend";
            string otpauthUri = $"otpauth://totp/{issuer}:{userId}?secret={base32Secret}&issuer={issuer}&digits=6";

            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);

            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            using var cmd = new MySqlCommand(@"
                UPDATE accounts 
                SET two_factor_secret = @secret 
                WHERE id = @id;
                ", connection);
            cmd.Parameters.AddWithValue("@secret", base32Secret);
            cmd.Parameters.AddWithValue("@id", userId);
            await cmd.ExecuteNonQueryAsync();

            return Results.Json(new { secret = base32Secret, qrCodeImage = Convert.ToBase64String(qrCodeImage) });
        });

        // move the logic to a service class later, since the handler / endpoint is too cluttered.
        group.MapPost("/verify-2fa", [Authorize(Policy = "Require2FAPending")] async (DbHelper dbHelper, HttpContext httpContext, JwtServices jwtService, TFAVerificationRequest request) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdClaim, out int userId);

            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            string? secret = null;
            using (var cmd = new MySqlCommand(@"
                SELECT two_factor_secret FROM accounts 
                WHERE id = @id;
                ", connection))
            {
                cmd.Parameters.AddWithValue("@id", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    secret = reader.IsDBNull(reader.GetOrdinal("two_factor_secret"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("two_factor_secret"));
                }
            }

            if (string.IsNullOrEmpty(secret))
            {
                return Results.BadRequest(new { message = "2FA is not set up for this account." });
            }

            var totp = new Totp(Base32Encoding.ToBytes(secret));
            bool isValid = totp.VerifyTotp(request.Token, out long timeStepMatched, VerificationWindow.RfcSpecifiedNetworkDelay);
            if (!isValid)
            {
                return Results.BadRequest(new { message = "Invalid 2FA token." });
            }

            var role = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
            var token = jwtService.GenerateToken(userId, role, "verified", 60);

            return Results.Json(new { message = "2FA VERIFICATION SUCCESSFUL!", token = token }, statusCode: 200);
        });
    }
}

