using MyAuthenticationBackend.Models;
using CsvHelper;
using CsvHelper.TypeConversion;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using BackendAuthentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MySql.Data.MySqlClient;
namespace AuthenticationBackend.Endpoints;
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/user").DisableAntiforgery();

        // perhaps make it modularized later if I have time.
        group.MapPost("/csv/student/upload", [Authorize] async (IFormFile? file, DbHelper dbHelper) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { message = "No file uploaded" });

            var successList = new List<StudentModel>();
            var failureList = new List<StudentResult>();

            List<StudentModel> records;
            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    TrimOptions = TrimOptions.Trim,
                    IgnoreBlankLines = true
                });
                records = csv.GetRecords<StudentModel>().ToList();
            }
            catch (TypeConverterException ex)
            {
                return Results.BadRequest(new { message = "CSV format is invalid: " + ex.Message });
            }
            catch (HeaderValidationException ex)
            {
                return Results.BadRequest(new { message = "CSV header is invalid: " + ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = "Error reading CSV file: " + ex.Message });
            }

            foreach (var record in records)
            {
                var validationErrors = new List<string>();

                // Validate required fields
                if (string.IsNullOrWhiteSpace(record.StudentId))
                    validationErrors.Add("StudentId is missing");
                else if (!long.TryParse(record.StudentId, out _))
                    validationErrors.Add("StudentId must be numeric");

                if (string.IsNullOrWhiteSpace(record.Name))
                    validationErrors.Add("First Name is missing");

                if (string.IsNullOrWhiteSpace(record.Surname))
                    validationErrors.Add("Last Name is missing");

                if (record.DateOfBirth == null)
                    validationErrors.Add("DateOfBirth is missing or invalid");

                if (string.IsNullOrWhiteSpace(record.Course))
                    validationErrors.Add("Course is missing");

                if (string.IsNullOrWhiteSpace(record.Gender))
                    validationErrors.Add("Gender is missing");

                if (string.IsNullOrWhiteSpace(record.CivilStatus))
                    validationErrors.Add("Civil Status is missing");

                if (string.IsNullOrWhiteSpace(record.YearLevel))
                    validationErrors.Add("YearLevel is missing");

                if (record.PhoneNumber?.Length > 20) // Arbitrary length limit
                    validationErrors.Add("PhoneNumber is too long");

                if (record.EmergencyContact?.Length > 20) // Arbitrary length limit
                    validationErrors.Add("EmergencyContact is too long");

                if (record.GuardianContact?.Length > 20) // Arbitrary length limit
                    validationErrors.Add("GuardianContact is too long");

                if (!long.TryParse(record.PhoneNumber, out _)) // Basic numeric check
                    validationErrors.Add("PhoneNumber must be numeric");

                if (!long.TryParse(record.EmergencyContact, out _)) // Basic numeric check
                    validationErrors.Add("EmergencyContact must be numeric");

                if (!long.TryParse(record.GuardianContact, out _)) // Basic numeric check
                    validationErrors.Add("GuardianContact must be numeric");

                if (validationErrors.Count > 0)
                {
                    failureList.Add(new StudentResult
                    {
                        StudentId = record.StudentId,
                        Name = record.Name,
                        Surname = record.Surname,
                        MiddleName = record.MiddleName ?? "",
                        DateOfBirth = record.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                        Course = record.Course,
                        Gender = record.Gender,
                        CivilStatus = record.CivilStatus,
                        YearLevel = record.YearLevel ?? "",
                        Section = record.Section ?? "",
                        Email = record.Email ?? "",
                        PhoneNumber = record.PhoneNumber ?? "",
                        Address = record.Address ?? "",
                        Nationality = record.Nationality ?? "",
                        Religion = record.Religion ?? "",
                        GuardianName = record.GuardianName ?? "",
                        GuardianContact = record.GuardianContact ?? "",
                        EmergencyContact = record.EmergencyContact ?? "",
                        AdmissionDate = record.AdmissionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                        GraduationDate = record.GraduationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                        GPA = record.GPA.ToString() ?? "",
                        Status = record.Status ?? "",
                        Scholarship = record.Scholarship ?? "",
                        Remarks = record.Remarks ?? "",
                        StudentType = record.StudentType ?? "",
                        Errors = validationErrors
                    });
                }
                else
                {
                    successList.Add(record);
                }
            }

            var insertResults = new List<StudentModel>();
            if (successList.Count > 0)
            {
                await using var connection = dbHelper.GetConnection();
                await connection.OpenAsync();

                var result = await dbHelper.BulkInsertStudentsAsync(successList, connection);
                insertResults.AddRange(result.inserted);

                result.skipped.ForEach(s => failureList.Add(new StudentResult
                {
                    StudentId = s.StudentId ?? "",
                    Name = s.Name ?? "",
                    Surname = s.Surname ?? "",
                    MiddleName = s.MiddleName ?? "",
                    DateOfBirth = s.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                    Course = s.Course ?? "",
                    Gender = s.Gender ?? "",
                    CivilStatus = s.CivilStatus ?? "",
                    YearLevel = s.YearLevel ?? "",
                    Section = s.Section ?? "",
                    Email = s.Email ?? "",
                    PhoneNumber = s.PhoneNumber ?? "",
                    Address = s.Address ?? "",
                    Nationality = s.Nationality ?? "",
                    Religion = s.Religion ?? "",
                    GuardianName = s.GuardianName ?? "",
                    GuardianContact = s.GuardianContact ?? "",
                    EmergencyContact = s.EmergencyContact ?? "",
                    AdmissionDate = s.AdmissionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                    GraduationDate = s.GraduationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                    GPA = s.GPA.ToString() ?? "",
                    Status = s.Status ?? "",
                    Scholarship = s.Scholarship ?? "",
                    Remarks = s.Remarks ?? "",
                    StudentType = s.StudentType ?? "",
                    Errors = new List<string> { "Duplicate StudentId" }
                }));
            }

            return Results.Ok(new
            {
                success = insertResults,
                failures = failureList
            });
        });

        // Too cluttered here. Maybe move to its own service later if I have time.
        group.MapGet("/me", [Authorize] async (HttpContext context, DbHelper dbHelper) =>
        {
            var identifierClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(identifierClaim) || !int.TryParse(identifierClaim, out int userId))
                return Results.Unauthorized();

            using var conn = dbHelper.GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT
                    a.id,
                    a.email,
                    a.last_login,
                    a.created_at,
                    u.first_name,
                    u.middle_name,
                    u.last_name
                FROM accounts a
                JOIN users u ON a.id = u.account_id
                WHERE a.id = @UserId
                LIMIT 1;
            ";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return Results.NotFound(new { message = "User not found" });
            }

            var user = new User
            {
                AccountId = reader.GetInt32(reader.GetOrdinal("id")),
                Email = reader.GetString(reader.GetOrdinal("email")),
                LastLogin = reader.IsDBNull(reader.GetOrdinal("last_login")) ? null : reader.GetDateTime(reader.GetOrdinal("last_login")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                MiddleName = reader.IsDBNull(reader.GetOrdinal("middle_name")) ? "" : reader.GetString(reader.GetOrdinal("middle_name")),
                LastName = reader.GetString(reader.GetOrdinal("last_name"))
            };

            return Results.Ok(new { message = "Success", user });
        });

        group.MapGet("/students", [Authorize] async (DbHelper dbHelper, HttpRequest request) =>
        {
            try
            {
                var connection = dbHelper.GetConnection();
                await connection.OpenAsync();

                // Get page and limit from query parameters
                int page = int.TryParse(request.Query["page"], out var p) && p > 0 ? p : 1;
                int limit = int.TryParse(request.Query["limit"], out var l) && l > 0 ? l : 10;
                int offset = (page - 1) * limit;

                List<StudentResult> students = new List<StudentResult>();

                // Fetch only the current page
                await using (var sql = new MySqlCommand("SELECT * FROM Students ORDER BY student_id DESC LIMIT @limit OFFSET @offset", connection))
                {
                    sql.Parameters.AddWithValue("@limit", limit);
                    sql.Parameters.AddWithValue("@offset", offset);

                    await using (var reader = await sql.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var student = new StudentResult
                            {
                                StudentId = reader.IsDBNull(reader.GetOrdinal("student_id")) ? "" : reader.GetString(reader.GetOrdinal("student_id")),
                                Name = reader.IsDBNull(reader.GetOrdinal("name")) ? "" : reader.GetString(reader.GetOrdinal("name")),
                                Surname = reader.IsDBNull(reader.GetOrdinal("surname")) ? "" : reader.GetString(reader.GetOrdinal("surname")),
                                DateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) ? "" : reader.GetDateTime(reader.GetOrdinal("date_of_birth")).ToString("yyyy-MM-dd"),
                                Course = reader.IsDBNull(reader.GetOrdinal("course")) ? "" : reader.GetString(reader.GetOrdinal("course")),
                                Gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? "" : reader.GetString(reader.GetOrdinal("gender")),
                                CivilStatus = reader.IsDBNull(reader.GetOrdinal("civil_status")) ? "" : reader.GetString(reader.GetOrdinal("civil_status")),
                                MiddleName = reader.IsDBNull(reader.GetOrdinal("middle_name")) ? "" : reader.GetString(reader.GetOrdinal("middle_name")),
                                YearLevel = reader.IsDBNull(reader.GetOrdinal("year_level")) ? "" : reader.GetString(reader.GetOrdinal("year_level")),
                                Section = reader.IsDBNull(reader.GetOrdinal("section")) ? "" : reader.GetString(reader.GetOrdinal("section")),
                                Email = reader.IsDBNull(reader.GetOrdinal("email")) ? "" : reader.GetString(reader.GetOrdinal("email")),
                                PhoneNumber = reader.IsDBNull(reader.GetOrdinal("phone_number")) ? "" : reader.GetString(reader.GetOrdinal("phone_number")),
                                Address = reader.IsDBNull(reader.GetOrdinal("address")) ? "" : reader.GetString(reader.GetOrdinal("address")),
                                Nationality = reader.IsDBNull(reader.GetOrdinal("nationality")) ? "" : reader.GetString(reader.GetOrdinal("nationality")),
                                Religion = reader.IsDBNull(reader.GetOrdinal("religion")) ? "" : reader.GetString(reader.GetOrdinal("religion")),
                                GuardianName = reader.IsDBNull(reader.GetOrdinal("guardian_name")) ? "" : reader.GetString(reader.GetOrdinal("guardian_name")),
                                GuardianContact = reader.IsDBNull(reader.GetOrdinal("guardian_contact")) ? "" : reader.GetString(reader.GetOrdinal("guardian_contact")),
                                EmergencyContact = reader.IsDBNull(reader.GetOrdinal("emergency_contact")) ? "" : reader.GetString(reader.GetOrdinal("emergency_contact")),
                                AdmissionDate = reader.IsDBNull(reader.GetOrdinal("admission_date")) ? "" : reader.GetDateTime(reader.GetOrdinal("admission_date")).ToString("yyyy-MM-dd"),
                                GraduationDate = reader.IsDBNull(reader.GetOrdinal("graduation_date")) ? "" : reader.GetDateTime(reader.GetOrdinal("graduation_date")).ToString("yyyy-MM-dd"),
                                GPA = reader.IsDBNull(reader.GetOrdinal("gpa")) ? "" : reader.GetDouble(reader.GetOrdinal("gpa")).ToString("0.##"),
                                Status = reader.IsDBNull(reader.GetOrdinal("status")) ? "" : reader.GetString(reader.GetOrdinal("status")),
                                Scholarship = reader.IsDBNull(reader.GetOrdinal("scholarship")) ? "" : reader.GetString(reader.GetOrdinal("scholarship")),
                                Remarks = reader.IsDBNull(reader.GetOrdinal("remarks")) ? "" : reader.GetString(reader.GetOrdinal("remarks")),
                                StudentType = reader.IsDBNull(reader.GetOrdinal("student_type")) ? "" : reader.GetString(reader.GetOrdinal("student_type"))
                            };

                            students.Add(student);
                        }
                    }
                }

                // Get total count for pagination info using a separate command
                int totalCount;
                await using (var countCmd = new MySqlCommand("SELECT COUNT(*) FROM Students", connection))
                {
                    totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                int totalPages = (int)Math.Ceiling((double)totalCount / limit);

                return Results.Ok(new
                {
                    students,
                    page,
                    totalPages,
                    totalCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Results.Json(
                    new { message = "Error retrieving students: " + ex.Message },
                    statusCode: 500
                );
            }
        });
    }
}