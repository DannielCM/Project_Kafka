using System.Data.SqlClient;
using MyAuthenticationBackend.Models;
using BackendAuthentication;
using MySql.Data.MySqlClient;

namespace MyAuthenticationBackend.Services;
public class AuditHelper
{
    private readonly DbHelper _dbHelper;

    // Store the DbHelper in a private field
    public AuditHelper(DbHelper dbHelper)
    {
        _dbHelper = dbHelper;
    }

    public async Task LogAsync(AuditEvent audit)
    {
        using (var connection = _dbHelper.GetConnection())
        {
            await connection.OpenAsync();

            string sql = @"
                INSERT INTO audit_logs 
                (user_id, action, Timestamp, Status)
                VALUES (@UserId, @Action, @Timestamp, @Status)";

            using (var cmd = new MySqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@UserId", audit.UserId);
                cmd.Parameters.AddWithValue("@Action", audit.Action);
                cmd.Parameters.AddWithValue("@Timestamp", audit.Timestamp);
                cmd.Parameters.AddWithValue("@Status", audit.Status ?? "Success");

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
