using MySql.Data.MySqlClient;
using MyAuthenticationBackend.Models;

namespace BackendAuthentication;
public class DbHelper
{
	private readonly string _connectionString;

	public DbHelper(string connectionString)
	{
		_connectionString = connectionString;
	}

	public MySqlConnection GetConnection()
	{
		return new MySqlConnection(_connectionString);
	}

    public (List<StudentModel> inserted, List<StudentModel> skipped) BulkInsertStudents(List<StudentModel> students, MySqlConnection _connection)
    {
        var insertedList = new List<StudentModel>();
        var skippedList = new List<StudentModel>();

        using var transaction = _connection.BeginTransaction();

        foreach (var student in students)
        {
            // Check if student already exists
            var checkCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM Students WHERE student_id = @StudentId",
                _connection,
                transaction);
            checkCmd.Parameters.AddWithValue("@StudentId", student.StudentId);

            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            if (exists)
            {
                skippedList.Add(student);
                continue; // skip inserting this student
            }

            // Insert student
            var cmd = new MySqlCommand(@"
                INSERT INTO Students (student_id, course, name, middle_name, surname, date_of_birth) 
                VALUES (@StudentId, @Course, @Name, @MiddleName, @Surname, @DateOfBirth)
            ", _connection, transaction);

            cmd.Parameters.AddWithValue("@StudentId", student.StudentId);
            cmd.Parameters.AddWithValue("@Course", student.Course);
            cmd.Parameters.AddWithValue("@Name", student.Name);
            cmd.Parameters.AddWithValue("@MiddleName", student.MiddleName ?? "");
            cmd.Parameters.AddWithValue("@Surname", student.Surname);
            cmd.Parameters.AddWithValue("@DateOfBirth", student.DateOfBirth);

            cmd.ExecuteNonQuery();
            insertedList.Add(student);
        }

        transaction.Commit();

        return (insertedList, skippedList);
    }
}
