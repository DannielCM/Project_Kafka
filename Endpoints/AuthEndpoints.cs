using MySql.Data.MySqlClient;
using BCrypt.Net;
using BackendAuthentication;

namespace AuthenticationBackend.Endpoints;
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/", () => "authentication route");
        group.MapPost("/login", async (DbHelper dbHelper, LogInRequest request) =>
        {
            request.Email = request.Email.Trim();
            request.Password = request.Password.Trim();

            using var conn = dbHelper.GetConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(@"
                SELECT * FROM accounts
                WHERE email = @email
                ", conn);
            cmd.Parameters.AddWithValue("@email", request.Email);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var password = reader.GetString(reader.GetOrdinal("password"));

                if (!BCrypt.Net.BCrypt.Verify(request.Password, password))
                {
                    return Results.Json(new { message = "INVALID CREDENTIALS!" }, statusCode: 401);
                }

                return Results.Json(new { message = "LOGIN SUCCESSFUL!" }, statusCode: 200);
            }
            else
            {
                return Results.Json(new { message = "INVALID CREDENTIALS!" }, statusCode: 401);
            }
        });
        group.MapPost("/register", async (DbHelper dbHelper, RegisterRequest request) =>
        {
            // validate data
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return Results.Json(new { message = "EMAIL, PASSWORD AND ROLE ARE REQUIRED!" }, statusCode: 400);
            }

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
                return Results.Json(new { message = "REGISTER FAILED!" }, statusCode: 500);
            }

            return Results.Json(new { message = "REGISTER SUCCESSFUL!" }, statusCode: 201);
        });
    }
}

