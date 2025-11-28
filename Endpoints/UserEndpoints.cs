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
using MyAuthenticationBackend.Services;

namespace AuthenticationBackend.Endpoints;
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/user").DisableAntiforgery();

        group.MapPost("/csv/students/template", async (DbHelper dbHelper, Template request) =>
        {
            var mapping = request.Map;
            if (mapping == null)
                return Results.BadRequest(new { message = "Invalid mapping object." });

            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Insert template row
                int id;
                using (var command = new MySqlCommand(
                    "INSERT INTO `template` (`name`) VALUES (@Name)", connection, transaction))
                {
                    command.Parameters.AddWithValue("@Name", request.Name);
                    await command.ExecuteNonQueryAsync();
                    id = (int)command.LastInsertedId;
                }

                // Build map values
                var mapValues = new List<Tuple<string, int>>();
                foreach (var entry in mapping)
                {
                    mapValues.Add(new Tuple<string, int>(entry.Key, entry.Value));
                }

                // Insert template_fields rows in a transaction
                using (var command = new MySqlCommand(
                    "INSERT INTO `template_fields` (`template_id`, `field`, `column_index`) VALUES (@TemplateId, @FieldName, @ColumnIndex)",
                    connection, transaction))
                {
                    foreach (var map in mapValues)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@TemplateId", id);
                        command.Parameters.AddWithValue("@FieldName", map.Item1);

                        // Safely convert dynamic to integer
                        int columnIndex;
                        try
                        {
                            columnIndex = Convert.ToInt32(map.Item2);
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            return Results.BadRequest(new
                            {
                                message = $"Invalid column index '{map.Item2}' for field '{map.Item1}'. Must be a number."
                            });
                        }

                        command.Parameters.AddWithValue("@ColumnIndex", columnIndex);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Commit everything
                await transaction.CommitAsync();
                return Results.Ok(new { message = "Template created successfully." });
            }
            catch (Exception ex)
            {
                // Rollback on any error
                await transaction.RollbackAsync();
                return Results.Problem(ex.Message);
            }
        });

        group.MapGet("/csv/students/templates", async (DbHelper dbHelper) =>
        {
            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            var templates = new List<Template>();

            using (var command = new MySqlCommand(
                @"SELECT template.created_at, template.id, template.name, template_fields.field, template_fields.column_index
                  FROM template
                  JOIN template_fields ON template_fields.template_id = template.id
                  ORDER BY template.id", connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                var templateDict = new Dictionary<int, Template>();

                while (await reader.ReadAsync())
                {
                    int templateId = reader.GetInt32(reader.GetOrdinal("id"));
                    string templateName = reader.GetString(reader.GetOrdinal("name"));
                    string field = reader.GetString(reader.GetOrdinal("field"));
                    int columnIndex = reader.GetInt32(reader.GetOrdinal("column_index"));

                    if (!templateDict.ContainsKey(templateId))
                    {
                        templateDict[templateId] = new Template
                        {
                            Id = templateId,
                            Name = templateName,
                            Map = new Dictionary<string, int>(),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                        };
                    }

                    templateDict[templateId].Map[field] = columnIndex;
                }

                templates = templateDict.Values.ToList();
            }

            return Results.Ok(new { templates });
        });

        group.MapDelete("/csv/students/template/{id}", async (int id, DbHelper dbHelper) =>
        {
            using var connection = dbHelper.GetConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // Delete template fields first due to foreign key constraint
                using (var command = new MySqlCommand(
                    "DELETE FROM template_fields WHERE template_id = @TemplateId", connection, transaction))
                {
                    command.Parameters.AddWithValue("@TemplateId", id);
                    await command.ExecuteNonQueryAsync();
                }

                // Delete the template
                using (var command = new MySqlCommand(
                    "DELETE FROM template WHERE id = @TemplateId", connection, transaction))
                {
                    command.Parameters.AddWithValue("@TemplateId", id);
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        await transaction.RollbackAsync();
                        return Results.NotFound(new { message = "Template not found." });
                    }
                }

                await transaction.CommitAsync();
                return Results.Ok(new { message = "Template deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await transaction.RollbackAsync();
                return Results.Problem(ex.Message);
            }
        });

        group.MapPost("/csv/students/upload", [Authorize] async (IFormFile? file, [FromForm] int templateId, DbHelper dbHelper, AuditHelper auditHelper, HttpContext httpContext) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { message = "No file uploaded" });

            try
            {
                using var connection = dbHelper.GetConnection();
                await connection.OpenAsync();

                // Fetch mapping for the selected template
                Dictionary<string, int> mapping = new();
                using (var command = new MySqlCommand(
                    "SELECT field, column_index FROM template_fields WHERE template_id = @TemplateId", connection))
                {
                    command.Parameters.AddWithValue("@TemplateId", templateId);

                    using var qreader = await command.ExecuteReaderAsync();
                    while (await qreader.ReadAsync())
                    {
                        string field = qreader.GetString(qreader.GetOrdinal("field"));
                        int columnIndex = qreader.GetInt32(qreader.GetOrdinal("column_index"));

                        mapping[field] = columnIndex;
                    }
                }

                if (mapping.Count == 0)
                    return Results.BadRequest(new { message = "Template mapping not found." });

                var students = new List<StudentModel>();

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

                    // StudentId
                    {
                        var sIdIdx = mapping["StudentId"];
                        student.StudentId = sIdIdx >= 0 && sIdIdx < dict.Count
                            ? dict.ElementAt(sIdIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (string.IsNullOrWhiteSpace(student.StudentId) || !long.TryParse(student.StudentId, out _))
                            student.Errors.Add("Student ID is required and must be numeric.");
                    }

                    // FirstName
                    {
                        var fIdx = mapping["FirstName"];
                        student.FirstName = fIdx >= 0 && fIdx < dict.Count
                            ? dict.ElementAt(fIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (string.IsNullOrWhiteSpace(student.FirstName))
                            student.Errors.Add("First Name is required.");
                    }

                    // MiddleName
                    {
                        var mIdx = mapping["MiddleName"];
                        student.MiddleName = mIdx >= 0 && mIdx < dict.Count
                            ? dict.ElementAt(mIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // SurName
                    {
                        var snIdx = mapping["SurName"];
                        student.SurName = snIdx >= 0 && snIdx < dict.Count
                            ? dict.ElementAt(snIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (string.IsNullOrWhiteSpace(student.SurName))
                            student.Errors.Add("Surname is required.");
                    }

                    // DateOfBirth
                    {
                        var dobIdx = mapping["DateOfBirth"];
                        student.DateOfBirth = dobIdx >= 0 && dobIdx < dict.Count
                            ? dict.ElementAt(dobIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.DateOfBirth) &&
                            !DateTime.TryParse(student.DateOfBirth, out _))
                            student.Errors.Add("DateOfBirth is invalid.");
                    }

                    // Gender
                    {
                        var gIdx = mapping["Gender"];
                        student.Gender = gIdx >= 0 && gIdx < dict.Count
                            ? dict.ElementAt(gIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.Gender))
                        {
                            var g = student.Gender.Trim().ToLower();
                            if (g != "male" && g != "female")
                                student.Errors.Add("Gender must be 'Male' or 'Female'.");
                        }
                    }

                    // CivilStatus
                    {
                        var csIdx = mapping["CivilStatus"];
                        student.CivilStatus = csIdx >= 0 && csIdx < dict.Count
                            ? dict.ElementAt(csIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // Nationality
                    {
                        var nIdx = mapping["Nationality"];
                        student.Nationality = nIdx >= 0 && nIdx < dict.Count
                            ? dict.ElementAt(nIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // Religion
                    {
                        var rIdx = mapping["Religion"];
                        student.Religion = rIdx >= 0 && rIdx < dict.Count
                            ? dict.ElementAt(rIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // BloodType
                    {
                        var btIdx = mapping["BloodType"];
                        student.BloodType = btIdx >= 0 && btIdx < dict.Count
                            ? dict.ElementAt(btIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // Course
                    {
                        var cIdx = mapping["Course"];
                        student.Course = cIdx >= 0 && cIdx < dict.Count
                            ? dict.ElementAt(cIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // YearLevel
                    {
                        var ylIdx = mapping["YearLevel"];
                        student.YearLevel = ylIdx >= 0 && ylIdx < dict.Count
                            ? dict.ElementAt(ylIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.YearLevel) &&
                            !int.TryParse(student.YearLevel, out _))
                            student.Errors.Add("YearLevel must be numeric.");
                    }

                    // Section
                    {
                        var secIdx = mapping["Section"];
                        student.Section = secIdx >= 0 && secIdx < dict.Count
                            ? dict.ElementAt(secIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // GPA
                    {
                        var gpaIdx = mapping["GPA"];
                        student.GPA = gpaIdx >= 0 && gpaIdx < dict.Count
                            ? dict.ElementAt(gpaIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.GPA) &&
                            !double.TryParse(student.GPA, out _))
                            student.Errors.Add("GPA must be numeric.");
                    }

                    // Status
                    {
                        var stIdx = mapping["Status"];
                        student.Status = stIdx >= 0 && stIdx < dict.Count
                            ? dict.ElementAt(stIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // Scholarship
                    {
                        var schIdx = mapping["Scholarship"];
                        student.Scholarship = schIdx >= 0 && schIdx < dict.Count
                            ? dict.ElementAt(schIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // Remarks
                    {
                        var remIdx = mapping["Remarks"];
                        student.Remarks = remIdx >= 0 && remIdx < dict.Count
                            ? dict.ElementAt(remIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // StudentType
                    {
                        var stypeIdx = mapping["StudentType"];
                        student.StudentType = stypeIdx >= 0 && stypeIdx < dict.Count
                            ? dict.ElementAt(stypeIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // LastEnrolledSemester
                    {
                        var lesIdx = mapping["LastEnrolledSemester"];
                        student.LastEnrolledSemester = lesIdx >= 0 && lesIdx < dict.Count
                            ? dict.ElementAt(lesIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // Email
                    {
                        var eIdx = mapping["Email"];
                        student.Email = eIdx >= 0 && eIdx < dict.Count
                            ? dict.ElementAt(eIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.Email))
                        {
                            try
                            {
                                var addr = new System.Net.Mail.MailAddress(student.Email);
                                if (addr.Address != student.Email)
                                    student.Errors.Add("Email is invalid.");
                            }
                            catch
                            {
                                student.Errors.Add("Email is invalid.");
                            }
                        }
                    }

                    // PhoneNumber
                    {
                        var pIdx = mapping["PhoneNumber"];
                        student.PhoneNumber = pIdx >= 0 && pIdx < dict.Count
                            ? dict.ElementAt(pIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.PhoneNumber) &&
                            !System.Text.RegularExpressions.Regex.IsMatch(student.PhoneNumber, @"^\d+$"))
                            student.Errors.Add("PhoneNumber is invalid.");
                    }

                    // Address
                    {
                        var addrIdx = mapping["Address"];
                        student.Address = addrIdx >= 0 && addrIdx < dict.Count
                            ? dict.ElementAt(addrIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // GuardianName
                    {
                        var gnameIdx = mapping["GuardianName"];
                        student.GuardianName = gnameIdx >= 0 && gnameIdx < dict.Count
                            ? dict.ElementAt(gnameIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // GuardianContact
                    {
                        var gcontIdx = mapping["GuardianContact"];
                        student.GuardianContact = gcontIdx >= 0 && gcontIdx < dict.Count
                            ? dict.ElementAt(gcontIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // EmergencyContact
                    {
                        var ecIdx = mapping["EmergencyContact"];
                        student.EmergencyContact = ecIdx >= 0 && ecIdx < dict.Count
                            ? dict.ElementAt(ecIdx).Value?.ToString()?.Trim() ?? ""
                            : "";
                    }

                    // AdmissionDate
                    {
                        var adIdx = mapping["AdmissionDate"];
                        student.AdmissionDate = adIdx >= 0 && adIdx < dict.Count
                            ? dict.ElementAt(adIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.AdmissionDate) &&
                            !DateTime.TryParse(student.AdmissionDate, out _))
                            student.Errors.Add("AdmissionDate is invalid.");
                    }

                    // GraduationDate
                    {
                        var gdIdx = mapping["GraduationDate"];
                        student.GraduationDate = gdIdx >= 0 && gdIdx < dict.Count
                            ? dict.ElementAt(gdIdx).Value?.ToString()?.Trim() ?? ""
                            : "";

                        if (!string.IsNullOrWhiteSpace(student.GraduationDate) &&
                            !DateTime.TryParse(student.GraduationDate, out _))
                            student.Errors.Add("GraduationDate is invalid.");
                    }

                    students.Add(student);
                }

                int.TryParse(httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId);
                await auditHelper.LogAsync(new AuditEvent
                {
                    UserId = userId,
                    Action = "Validated student CSV",
                    Timestamp = DateTime.UtcNow,
                    Status = "Success"
                });

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