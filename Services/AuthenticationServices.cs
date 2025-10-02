using BackendAuthentication;
using Microsoft.Extensions.Configuration;
using MyAuthenticationBackend.Models;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace MyAuthenticationBackend.AppServices;
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

    public async Task<LoginResult> AuthenticateUser(LogInRequest request)
    {
        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        using var cmd = new MySqlCommand("SELECT * FROM accounts WHERE email = @email", conn);
        cmd.Parameters.AddWithValue("@email", request.Email);

        int id;
        string stored_hash;
        string role;
        string? twoFactorSecret;

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            id = reader.GetInt32(reader.GetOrdinal("id"));
            stored_hash = reader.GetString(reader.GetOrdinal("password"));
            role = reader.GetString(reader.GetOrdinal("role"));
            twoFactorSecret = reader.IsDBNull(reader.GetOrdinal("two_factor_secret"))
                ? null
                : reader.GetString(reader.GetOrdinal("two_factor_secret"));
        }
        else
        {
            return new LoginResult { Success = false, Message = "INVALID CREDENTIALS" };
        }

        reader.Close();

        if (!BCrypt.Net.BCrypt.Verify(request.Password, stored_hash))
        {
            return new LoginResult { Success = false, Message = "INVALID CREDENTIALS" };
        }

        if (twoFactorSecret != null)
        {
            var token = _jwtService.GenerateToken(id, role, "pending", 5);
            var redirectUrl = _config["Auth:TwoFactorRedirectUrl"];

            return new LoginResult
            {
                Success = true,
                Token = token,
                RedirectUrl = redirectUrl,
                Message = "2FA REQUIRED"
            };
        }

        var finalToken = _jwtService.GenerateToken(id, role, "verified", 60);

        using var updateCmd = new MySqlCommand("UPDATE accounts SET last_login = NOW() WHERE email = @email", conn);
        updateCmd.Parameters.AddWithValue("@email", request.Email);
        await updateCmd.ExecuteNonQueryAsync();

        return new LoginResult
        {
            Success = true,
            Token = finalToken,
            Message = "LOGIN SUCCESSFUL"
        };
    }

    public async Task<RegisterResults> RegisterUser(RegisterRequest request)
    {
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
