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
using System.Text.Json;

namespace AuthenticationBackend.Endpoints;
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/user").DisableAntiforgery();

        group.MapPost("/csv/students/upload", async (IFormFile? file, [FromForm] string map) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { message = "No file uploaded" });

            var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(map);
            if (mapping == null)
                return Results.BadRequest(new { message = "Invalid mapping object." });

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
                    var record = csv.GetRecord<dynamic>();
                    var dict = (IDictionary<string, object>)record;

                    var student = new StudentModel();

                    student.StudentId = int.TryParse(mapping["StudentId"], out var sIdIdx) && sIdIdx >= 0 && sIdIdx < dict.Count
                        ? dict.ElementAt(sIdIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(student.StudentId) || !long.TryParse(student.StudentId, out _))
                        student.Errors.Add("Student ID is required and must be numeric.");

                    student.FirstName = int.TryParse(mapping["FirstName"], out var fIdx) && fIdx >= 0 && fIdx < dict.Count
                        ? dict.ElementAt(fIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(student.FirstName))
                        student.Errors.Add("First Name is required.");

                    student.MiddleName = int.TryParse(mapping["MiddleName"], out var mIdx) && mIdx >= 0 && mIdx < dict.Count
                        ? dict.ElementAt(mIdx).Value?.ToString()?.Trim() ?? "" : "";

                    student.SurName = int.TryParse(mapping["SurName"], out var snIdx) && snIdx >= 0 && snIdx < dict.Count
                        ? dict.ElementAt(snIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(student.SurName))
                        student.Errors.Add("Surname is required.");

                    student.DateOfBirth = int.TryParse(mapping["DateOfBirth"], out var dobIdx) && dobIdx >= 0 && dobIdx < dict.Count
                        ? dict.ElementAt(dobIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.DateOfBirth) && !DateTime.TryParse(student.DateOfBirth, out _))
                        student.Errors.Add("DateOfBirth is invalid.");

                    student.Gender = int.TryParse(mapping["Gender"], out var gIdx) && gIdx >= 0 && gIdx < dict.Count
                        ? dict.ElementAt(gIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.Gender))
                    {
                        var g = student.Gender.Trim().ToLower();
                        if (g != "male" && g != "female")
                            student.Errors.Add("Gender must be 'Male' or 'Female'.");
                    }

                    student.CivilStatus = int.TryParse(mapping["CivilStatus"], out var csIdx) && csIdx >= 0 && csIdx < dict.Count
                        ? dict.ElementAt(csIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.Nationality = int.TryParse(mapping["Nationality"], out var nIdx) && nIdx >= 0 && nIdx < dict.Count
                        ? dict.ElementAt(nIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.Religion = int.TryParse(mapping["Religion"], out var rIdx) && rIdx >= 0 && rIdx < dict.Count
                        ? dict.ElementAt(rIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.BloodType = int.TryParse(mapping["BloodType"], out var btIdx) && btIdx >= 0 && btIdx < dict.Count
                        ? dict.ElementAt(btIdx).Value?.ToString()?.Trim() ?? "" : "";

                    student.Course = int.TryParse(mapping["Course"], out var cIdx) && cIdx >= 0 && cIdx < dict.Count
                        ? dict.ElementAt(cIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.YearLevel = int.TryParse(mapping["YearLevel"], out var ylIdx) && ylIdx >= 0 && ylIdx < dict.Count
                        ? dict.ElementAt(ylIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.YearLevel) && !int.TryParse(student.YearLevel, out _))
                        student.Errors.Add("YearLevel must be numeric.");

                    student.Section = int.TryParse(mapping["Section"], out var secIdx) && secIdx >= 0 && secIdx < dict.Count
                        ? dict.ElementAt(secIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.GPA = int.TryParse(mapping["GPA"], out var gpaIdx) && gpaIdx >= 0 && gpaIdx < dict.Count
                        ? dict.ElementAt(gpaIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.GPA) && !double.TryParse(student.GPA, out _))
                        student.Errors.Add("GPA must be numeric.");

                    student.Status = int.TryParse(mapping["Status"], out var stIdx) && stIdx >= 0 && stIdx < dict.Count
                        ? dict.ElementAt(stIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.Scholarship = int.TryParse(mapping["Scholarship"], out var schIdx) && schIdx >= 0 && schIdx < dict.Count
                        ? dict.ElementAt(schIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.Remarks = int.TryParse(mapping["Remarks"], out var remIdx) && remIdx >= 0 && remIdx < dict.Count
                        ? dict.ElementAt(remIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.StudentType = int.TryParse(mapping["StudentType"], out var stypeIdx) && stypeIdx >= 0 && stypeIdx < dict.Count
                        ? dict.ElementAt(stypeIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.LastEnrolledSemester = int.TryParse(mapping["LastEnrolledSemester"], out var lesIdx) && lesIdx >= 0 && lesIdx < dict.Count
                        ? dict.ElementAt(lesIdx).Value?.ToString()?.Trim() ?? "" : "";

                    student.Email = int.TryParse(mapping["Email"], out var eIdx) && eIdx >= 0 && eIdx < dict.Count
                        ? dict.ElementAt(eIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.Email))
                    {
                        try { var addr = new System.Net.Mail.MailAddress(student.Email); if (addr.Address != student.Email) student.Errors.Add("Email is invalid."); }
                        catch { student.Errors.Add("Email is invalid."); }
                    }

                    student.PhoneNumber = int.TryParse(mapping["PhoneNumber"], out var pIdx) && pIdx >= 0 && pIdx < dict.Count
                        ? dict.ElementAt(pIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.PhoneNumber) && !System.Text.RegularExpressions.Regex.IsMatch(student.PhoneNumber, @"^\d+$"))
                        student.Errors.Add("PhoneNumber is invalid.");

                    student.Address = int.TryParse(mapping["Address"], out var addrIdx) && addrIdx >= 0 && addrIdx < dict.Count
                        ? dict.ElementAt(addrIdx).Value?.ToString()?.Trim() ?? "" : "";

                    student.GuardianName = int.TryParse(mapping["GuardianName"], out var gnameIdx) && gnameIdx >= 0 && gnameIdx < dict.Count
                        ? dict.ElementAt(gnameIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.GuardianContact = int.TryParse(mapping["GuardianContact"], out var gcontIdx) && gcontIdx >= 0 && gcontIdx < dict.Count
                        ? dict.ElementAt(gcontIdx).Value?.ToString()?.Trim() ?? "" : "";
                    student.EmergencyContact = int.TryParse(mapping["EmergencyContact"], out var ecIdx) && ecIdx >= 0 && ecIdx < dict.Count
                        ? dict.ElementAt(ecIdx).Value?.ToString()?.Trim() ?? "" : "";

                    student.AdmissionDate = int.TryParse(mapping["AdmissionDate"], out var adIdx) && adIdx >= 0 && adIdx < dict.Count
                        ? dict.ElementAt(adIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.AdmissionDate) && !DateTime.TryParse(student.AdmissionDate, out _))
                        student.Errors.Add("AdmissionDate is invalid.");

                    student.GraduationDate = int.TryParse(mapping["GraduationDate"], out var gdIdx) && gdIdx >= 0 && gdIdx < dict.Count
                        ? dict.ElementAt(gdIdx).Value?.ToString()?.Trim() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(student.GraduationDate) && !DateTime.TryParse(student.GraduationDate, out _))
                        student.Errors.Add("GraduationDate is invalid.");

                    students.Add(student);
                }

                return Results.Ok(new { results = students });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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