using MyAuthenticationBackend.Models;
using CsvHelper;
using CsvHelper.TypeConversion;
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
        group.MapPost("/csv/student/upload", async (IFormFile file, DbHelper dbHelper) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "No file uploaded" });
            }

            var successList = new List<StudentModel>();
            var failureList = new List<StudentCSVModel>();

            var records = new List<StudentModel>();
            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
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

                // Check essential NOT NULL fields
                if (string.IsNullOrWhiteSpace(record.StudentId))
                {
                    validationErrors.Add("StudentId is missing");
                }
                else if (!int.TryParse(record.StudentId, out _))
                {
                    validationErrors.Add("StudentId must be numeric");
                }

                if (string.IsNullOrWhiteSpace(record.Name))
                {
                    validationErrors.Add("First Name is missing");
                }

                if (string.IsNullOrWhiteSpace(record.Surname))
                {
                    validationErrors.Add("Last Name is missing");
                }

                if (string.IsNullOrWhiteSpace(record.DateOfBirth) ||
                    !DateTime.TryParseExact(record.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    validationErrors.Add("DateOfBirth is missing or invalid");
                }

                if (string.IsNullOrWhiteSpace(record.Course))
                {
                    validationErrors.Add("Course is missing");
                }

                // New NOT NULL fields
                if (string.IsNullOrWhiteSpace(record.Gender))
                {
                    validationErrors.Add("Gender is missing");
                }

                if (string.IsNullOrWhiteSpace(record.CivilStatus))
                {
                    validationErrors.Add("Civil Status is missing");
                }

                if (string.IsNullOrWhiteSpace(record.YearLevel))
                {
                    validationErrors.Add("YearLevel is missing");
                }

                // Add to success or failure
                if (validationErrors.Count > 0)
                {
                    failureList.Add(new StudentCSVModel
                    {
                        StudentId = record.StudentId,
                        Name = record.Name,
                        Surname = record.Surname,
                        MiddleName = record.MiddleName ?? "",
                        DateOfBirth = record.DateOfBirth,
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
                        AdmissionDate = record.AdmissionDate ?? "",
                        GraduationDate = record.GraduationDate ?? "",
                        GPA = record.GPA ?? "",
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
                var connection = dbHelper.GetConnection();
                await connection.OpenAsync();

                var result = await dbHelper.BulkInsertStudentsAsync(successList, connection);

                insertResults.AddRange(result.inserted);

                result.skipped.ForEach(s => failureList.Add(new StudentCSVModel
                {
                    StudentId = s.StudentId ?? "",
                    Name = s.Name ?? "",
                    Surname = s.Surname ?? "",
                    MiddleName = s.MiddleName ?? "",
                    DateOfBirth = s.DateOfBirth ?? "",
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
                    AdmissionDate = s.AdmissionDate ?? "",
                    GraduationDate = s.GraduationDate ?? "",
                    GPA = s.GPA ?? "",
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
    }
}