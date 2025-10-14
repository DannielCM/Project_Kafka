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

    public async Task<(List<StudentModel> inserted, List<StudentModel> skipped)> BulkInsertStudentsAsync(
    List<StudentModel> students,
    MySqlConnection _connection)
    {
        var insertedList = new List<StudentModel>();
        var skippedList = new List<StudentModel>();

        await using var transaction = await _connection.BeginTransactionAsync();

        try
        {
            foreach (var student in students)
            {
                // Check if student already exists
                using var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM Students WHERE student_id = @StudentId",
                    _connection,
                    transaction);
                checkCmd.Parameters.AddWithValue("@StudentId", student.StudentId);

                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                if (exists)
                {
                    skippedList.Add(student);
                    continue;
                }

                // Insert student
                using var cmd = new MySqlCommand(@"
                    INSERT INTO Students 
                        (student_id, course, name, middle_name, surname, date_of_birth, year_level, section, email, phone_number, address, gender, nationality, religion, civil_status, guardian_name, guardian_contact, emergency_contact, admission_date, graduation_date, gpa, status, scholarship, remarks, student_type)
                    VALUES 
                        (@StudentId, @Course, @Name, @MiddleName, @Surname, @DateOfBirth, @YearLevel, @Section, @Email, @PhoneNumber, @Address, @Gender, @Nationality, @Religion, @CivilStatus, @GuardianName, @GuardianContact, @EmergencyContact, @AdmissionDate, @GraduationDate, @GPA, @Status, @Scholarship, @Remarks, @StudentType)
                ", _connection, transaction);

                cmd.Parameters.AddWithValue("@StudentId", student.StudentId);
                cmd.Parameters.AddWithValue("@Course", student.Course);
                cmd.Parameters.AddWithValue("@Name", student.Name);
                cmd.Parameters.AddWithValue("@MiddleName", student.MiddleName ?? "");
                cmd.Parameters.AddWithValue("@Surname", student.Surname);
                cmd.Parameters.AddWithValue("@DateOfBirth", student.DateOfBirth);
                cmd.Parameters.AddWithValue("@YearLevel", student.YearLevel);
                cmd.Parameters.AddWithValue("@Section", student.Section ?? "");
                cmd.Parameters.AddWithValue("@Email", student.Email ?? "");
                cmd.Parameters.AddWithValue("@PhoneNumber", student.PhoneNumber ?? "");
                cmd.Parameters.AddWithValue("@Address", student.Address ?? "");
                cmd.Parameters.AddWithValue("@Gender", student.Gender);
                cmd.Parameters.AddWithValue("@Nationality", student.Nationality ?? "");
                cmd.Parameters.AddWithValue("@Religion", student.Religion ?? "");
                cmd.Parameters.AddWithValue("@CivilStatus", student.CivilStatus);
                cmd.Parameters.AddWithValue("@GuardianName", student.GuardianName ?? "");
                cmd.Parameters.AddWithValue("@GuardianContact", student.GuardianContact ?? "");
                cmd.Parameters.AddWithValue("@EmergencyContact", student.EmergencyContact ?? "");
                cmd.Parameters.AddWithValue("@AdmissionDate", (student.AdmissionDate == null) ? DBNull.Value : student.AdmissionDate);
                cmd.Parameters.AddWithValue("@GraduationDate", (student.GraduationDate == null) ? DBNull.Value : student.GraduationDate);
                cmd.Parameters.AddWithValue("@GPA", (student.GPA <= 0) ? DBNull.Value : student.GPA);
                cmd.Parameters.AddWithValue("@Status", student.Status ?? "");
                cmd.Parameters.AddWithValue("@Scholarship", student.Scholarship ?? "");
                cmd.Parameters.AddWithValue("@Remarks", student.Remarks ?? "");
                cmd.Parameters.AddWithValue("@StudentType", student.StudentType ?? "");

                await cmd.ExecuteNonQueryAsync();
                insertedList.Add(student);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return (insertedList, skippedList);
    }
}
