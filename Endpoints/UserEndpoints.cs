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
using System.Reflection;
namespace AuthenticationBackend.Endpoints;
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/user").DisableAntiforgery();

        group.MapPost("/csv/students/upload", [Authorize] async (IFormFile? file) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "No file uploaded" });
            }

            var students = new List<StudentModel>();

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    TrimOptions = TrimOptions.Trim,
                    IgnoreBlankLines = true
                });

                while (await csv.ReadAsync())
                {
                    var records = csv.GetRecord<dynamic>();
                    var dict = (IDictionary<String, Object>)records;

                    StudentModel? student = null;
                    if (file.FileName == "100_students.csv") {
                        student = new StudentModel()
                        {
                            StudentId = dict.ElementAt(0).Value?.ToString()?.Trim() ?? "",
                            FirstName = dict.ElementAt(1).Value?.ToString()?.Trim() ?? "",
                            MiddleName = dict.ElementAt(2).Value?.ToString()?.Trim() ?? "",
                            SurName = dict.ElementAt(3).Value?.ToString()?.Trim() ?? "",
                            DateOfBirth = dict.ElementAt(4).Value?.ToString()?.Trim() ?? "",
                            Gender = dict.ElementAt(5).Value?.ToString()?.Trim() ?? "",
                            CivilStatus = dict.ElementAt(6).Value?.ToString()?.Trim() ?? "",
                            Nationality = dict.ElementAt(7).Value?.ToString()?.Trim() ?? "",
                            Religion = dict.ElementAt(8).Value?.ToString()?.Trim() ?? "",
                            BloodType = dict.ElementAt(9).Value?.ToString()?.Trim() ?? "",
                            Course = dict.ElementAt(10).Value?.ToString()?.Trim() ?? "",
                            YearLevel = dict.ElementAt(11).Value?.ToString()?.Trim() ?? "",
                            Section = dict.ElementAt(12).Value?.ToString()?.Trim() ?? "",
                            GPA = dict.ElementAt(13).Value?.ToString()?.Trim() ?? "",
                            Status = dict.ElementAt(14).Value?.ToString()?.Trim() ?? "",
                            Scholarship = dict.ElementAt(15).Value?.ToString()?.Trim() ?? "",
                            Remarks = dict.ElementAt(16).Value?.ToString()?.Trim() ?? "",
                            StudentType = dict.ElementAt(17).Value?.ToString()?.Trim() ?? "",
                            LastEnrolledSemester = dict.ElementAt(18).Value?.ToString()?.Trim() ?? "",
                            Email = dict.ElementAt(19).Value?.ToString()?.Trim() ?? "",
                            PhoneNumber = dict.ElementAt(20).Value?.ToString()?.Trim() ?? "",
                            Address = dict.ElementAt(21).Value?.ToString()?.Trim() ?? "",
                            GuardianName = dict.ElementAt(22).Value?.ToString()?.Trim() ?? "",
                            GuardianContact = dict.ElementAt(23).Value?.ToString()?.Trim() ?? "",
                            EmergencyContact = dict.ElementAt(24).Value?.ToString()?.Trim() ?? "",
                            AdmissionDate = dict.ElementAt(25).Value?.ToString()?.Trim() ?? "",
                            GraduationDate = dict.ElementAt(26).Value?.ToString()?.Trim() ?? ""
                        };

                        if (string.IsNullOrEmpty(student.StudentId) || !long.TryParse(student.StudentId, out long id))
                        {
                            student.Errors.Add("Student ID must be provided and must be in numberic form");
                        }
                    }

                    students.Add(student);
                }

                return Results.Ok(new { results = students });
            }
            catch (Exception e) 
            {
                Console.WriteLine(e.Message);
                return Results.Problem("Internal Server Error");
            }
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