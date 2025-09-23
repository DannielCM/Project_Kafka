using MySql.Data.MySqlClient;
using BackendAuthentication;
using BCrypt.Net;

namespace MyAuthenticationBackend.AppServices;
public class UserServices
{
	private readonly DbHelper _dbHelper;

	public UserServices(DbHelper dbHelper)
	{
		_dbHelper = dbHelper;
	}

	public async Task<bool> resetPasswordAsync(int id, string currentPassword, string newPassword, string newPasswordConfirmation) {
		if (newPassword != newPasswordConfirmation)
			throw new ArgumentException("Passwords do not match.");

		using var conn = _dbHelper.GetConnection();
		await conn.OpenAsync();

		using var storedHashcmd = new MySqlCommand(@"
			SELECT password 
			FROM accounts 
			WHERE id = @Id
		", conn);
		storedHashcmd.Parameters.AddWithValue("@Id", id);
		
		using (var reader = await storedHashcmd.ExecuteReaderAsync())
		{
			if (await reader.ReadAsync())
			{
				string storedHash = reader.GetString(reader.GetOrdinal("password"));

				bool isValid = BCrypt.Net.BCrypt.Verify(currentPassword, storedHash);
				if (!isValid)
					throw new ArgumentException("Current password is incorrect.");

				bool isSamePassword = BCrypt.Net.BCrypt.Verify(newPassword, storedHash);
				if (isSamePassword)
					throw new ArgumentException("New password cannot be the same as the current password.");
			}
			else
			{
				throw new ArgumentException("User not found.");
			}
		}

		string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
		using var updatePasswordcmd = new MySqlCommand(@"
			UPDATE accounts 
			SET password = @NewHash WHERE id = @Id
		", conn);
		updatePasswordcmd.Parameters.AddWithValue("@NewHash", hashedPassword);
		updatePasswordcmd.Parameters.AddWithValue("@Id", id);

		int rowsAffected = await updatePasswordcmd.ExecuteNonQueryAsync();

		if (rowsAffected == 0)
			throw new Exception("No rows were updated.");

		return true;
	}
}