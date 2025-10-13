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
        group.MapPost("/2fa/setup", [Authorize] async (IConfiguration config, DbHelper dbHelper, HttpContext httpContext, AuthenticationServices authService, TFASetupRequest request) =>
        {
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdClaim, out int userId);

            try
            {
                var result = await authService.EnableTFA(request.Enabled, email, userId);
                if (!result.Success)
                {
                    return Results.BadRequest(new { message = result.Message ?? "2FA SETUP FAILED!" });
                }

                return Results.Json(new
                {
                    Enabled = request.Enabled == 1,
                    Secret = result.ManualEntryKey,
                    QrCodeImage = result.QRCodeImageBase64,
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return Results.Json(new { message = "INTERNAL SERVER ERROR!" }, statusCode: 500);
            }
        });

        // move the logic to a service class later, since the handler / endpoint is too cluttered.
        group.MapPost("/2fa/verify", async (DbHelper dbHelper, KafkaProducerService kafkaProducerService, AuthenticationServices authService, JwtServices jwtService, TFAVerificationRequest request) =>
        {
            try
            {
                var result = await authService.VerifyTFA(request.Token, request.Code);
                if (!result.IsSuccess)
                {
                    return Results.BadRequest(new { message = result.Message ?? "2FA VERIFICATION FAILED!" });
                }

                var access_token = jwtService.GenerateToken(result.Account.Id, result.Account.Email, result.Account.Role, 60);

                kafkaProducerService.SendLoginMessage($"{result.Account.Id}");

                return Results.Json(new { message = "2FA VERIFICATION SUCCESSFUL!", token = access_token }, statusCode: 200);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return Results.Json(new { message = "INTERNAL SERVER ERROR!" }, statusCode: 500);
            }
        });
    }
}

