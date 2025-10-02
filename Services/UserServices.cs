using MySql.Data.MySqlClient;
using BackendAuthentication;
using BCrypt.Net;
using MyAuthenticationBackend.Models;

namespace MyAuthenticationBackend.AppServices;
public class UserServices
{
	private readonly DbHelper _dbHelper;

	public UserServices(DbHelper dbHelper)
	{
		_dbHelper = dbHelper;
	}

	public async Task<PasswordResetResults> ChangePassword(int id, string currentPassword, string newPassword, string newPasswordConfirmation) {
        if (newPassword != newPasswordConfirmation)
        {
            return new PasswordResetResults
            {
                Success = false,
                Message = "New password and confirmed password do not match."
            };
        }

        using var conn = _dbHelper.GetConnection();
        await conn.OpenAsync();

        string storedHash;

        using (var storedHashcmd = new MySqlCommand(@"
            SELECT password 
            FROM accounts 
            WHERE id = @Id
        ", conn))
        {
            storedHashcmd.Parameters.AddWithValue("@Id", id);

            using var reader = await storedHashcmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new PasswordResetResults
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            storedHash = reader.GetString(reader.GetOrdinal("password"));
        }

        // Validate current password
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, storedHash))
        {
            return new PasswordResetResults
            {
                Success = false,
                Message = "Current password is incorrect."
            };
        }

        // Check if new password is same as current
        if (BCrypt.Net.BCrypt.Verify(newPassword, storedHash))
        {
            return new PasswordResetResults
            {
                Success = false,
                Message = "New password cannot be the same as the current password."
            };
        }

        // Update password
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        using var updatePasswordcmd = new MySqlCommand(@"
            UPDATE accounts 
            SET password = @NewHash 
            WHERE id = @Id
        ", conn);
        updatePasswordcmd.Parameters.AddWithValue("@NewHash", hashedPassword);
        updatePasswordcmd.Parameters.AddWithValue("@Id", id);

        int rowsAffected = await updatePasswordcmd.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
        {
            return new PasswordResetResults
            {
                Success = false,
                Message = "Password was not updated."
            };
        }

        return new PasswordResetResults
        {
            Success = true,
            Message = "Password has been successfully updated."
        };
    }
}