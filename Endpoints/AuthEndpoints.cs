using MySql.Data.MySqlClient;
using BCrypt.Net;
using BackendAuthentication;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Confluent.Kafka;

namespace AuthenticationBackend.Endpoints;
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/", () => "authentication route");
        group.MapPost("/login", async (DbHelper dbHelper, IProducer<Null, string> producer, IConfiguration config, LogInRequest request) =>
        {
            try
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
                    var id = reader.GetInt32(reader.GetOrdinal("id"));
                    var password = reader.GetString(reader.GetOrdinal("password"));
                    var role = reader.GetString(reader.GetOrdinal("role"));

                    if (!BCrypt.Net.BCrypt.Verify(request.Password, password))
                    {
                        return Results.Json(new { message = "INVALID CREDENTIALS!" }, statusCode: 401);
                    }

                    // write claim/body of the token
                    var claims = new[] {
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Role, role)
                };

                    // create key for signing the token
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));

                    // signing algorithm used to generate the token
                    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                    var token = new JwtSecurityToken(
                        issuer: config["Jwt:Issuer"],
                        audience: config["Jwt:Audience"],
                        claims: claims,
                        expires: DateTime.UtcNow.AddHours(1),
                        signingCredentials: creds
                    );

                    // convert my token object to string for a standard JWT format
                    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                    try
                    {
                        var metadata = producer.GetMetadata(TimeSpan.FromSeconds(1));
                        if (metadata.Brokers.Count > 0)
                        {
                            var message = new Message<Null, string> { Value = $"user {id} has logged in!" };

                            producer.Produce("test-topic", message, (deliveryReport) =>
                            {
                                if (deliveryReport.Error.IsError)
                                    Console.Error.WriteLine($"Kafka delivery failed: {deliveryReport.Error.Reason}");
                                else
                                    Console.WriteLine($"Kafka message delivered to {deliveryReport.TopicPartitionOffset}");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Unexpected Kafka error: {ex}");
                    }

                    return Results.Json(new { message = "LOGIN SUCCESSFUL!", token = tokenString }, statusCode: 200);
                }

                return Results.Json(new { message = "INVALID CREDENTIALS!" }, statusCode: 401);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return Results.Json(new { message = "INTERNAL SERVER ERROR!" }, statusCode: 500);
            }
        });
        group.MapPost("/register", async (DbHelper dbHelper, RegisterRequest request) =>
        {
            try
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
            } 
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return Results.Json(new { message = "INTERNAL SERVER ERROR!" }, statusCode: 500);
            }
        });
    }
}

