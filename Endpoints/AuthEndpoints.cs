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

namespace AuthenticationBackend.Endpoints;
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/", () => "authentication route");
        group.MapPost("/login", async (DbHelper dbHelper, IConfiguration config, IMemoryCache cache, JwtServices jwtService, KafkaProducerService kafkaService, LogInRequest request) =>
        {
            try
            {
                if (!cache.TryGetValue(request.CaptchaId, out string storedText) || !storedText.Equals(request.CaptchaText, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { success = false, message = "CAPTCHA incorrect" });
                }
                cache.Remove(request.CaptchaId);

                request.Email = request.Email.Trim();
                request.Password = request.Password.Trim();

                using var conn = dbHelper.GetConnection();
                await conn.OpenAsync();

                using var getAccountcmd = new MySqlCommand(@"
                SELECT * FROM accounts
                WHERE email = @email
                ", conn);
                getAccountcmd.Parameters.AddWithValue("@email", request.Email);

                int id;
                string password;
                string role;
                using (var reader = await getAccountcmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id"));
                        password = reader.GetString(reader.GetOrdinal("password"));
                        role = reader.GetString(reader.GetOrdinal("role"));
                    }
                    else
                    {
                        return Results.Json(new { message = "INVALID CREDENTIALS!" }, statusCode: 401);
                    }
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, password))
                {
                    return Results.Json(new { message = "INVALID CREDENTIALS!" }, statusCode: 401);
                }

                var token = jwtService.GenerateToken(id, role);

                using var updateLastLogincmd = new MySqlCommand(@"
                UPDATE accounts 
                SET last_login = NOW() 
                WHERE email = @email;
                ", conn);
                updateLastLogincmd.Parameters.AddWithValue("@email", request.Email);

                int rowsAffected = await updateLastLogincmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new Exception("Failed to update last login!");
                }

                kafkaService.SendLoginMessage(id);

                return Results.Json(new { message = "LOGIN SUCCESSFUL!", token = token }, statusCode: 200);
            }
            catch (Exception ex)
            {
                return Results.Json(new { message = "Internal server error." }, statusCode: 500);
            }
        });
        group.MapPost("/register", async (DbHelper dbHelper, RegisterRequest request) =>
        {
            try
            {
                request.Email = request.Email.Trim();
                request.Password = request.Password.Trim();
                request.Role = request.Role?.Trim();

                using var conn = dbHelper.GetConnection();
                await conn.OpenAsync();

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

                return Results.Json(new { message = "REGISTER SUCCESSFUL!" }, statusCode: 201);
            } 
            catch (Exception ex)
            {
                return Results.Json(new { message = ex.Message ?? "INTERNAL SERVER ERROR!" }, statusCode: 500);
            }
        });
        group.MapPost("/reset-password", [Authorize] async (DbHelper dbHelper, HttpContext httpContext, UserServices userServices, ResetPasswordRequest request) =>
        {
            try
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(userIdClaim, out int userId);
                var passwordReset = await userServices.resetPasswordAsync(userId, request.CurrentPassword, request.NewPassword, request.NewPasswordConfirmation);

                return Results.Ok(new { message = "PASSWORD RESET SUCCESSFUL!" });
            }
            catch (ArgumentException argEx)
            {
                return Results.BadRequest(new { message = argEx.Message });
            }
            catch (Exception ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: 500);
            }
        });
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
    }
}

