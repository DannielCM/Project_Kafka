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

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    TrimOptions = TrimOptions.Trim,
                    IgnoreBlankLines = true
                });

                var map = new Dictionary<string, string>()
                {
                    // Primary Identifier
                    ["studentid"] = "StudentId",
                    ["id"] = "StudentId",

                    // Personal Information
                    ["name"] = "Name",
                    ["full name"] = "Name",
                    ["f name"] = "Name",
                    ["middlename"] = "MiddleName",
                    ["middle name"] = "MiddleName",
                    ["surname"] = "Surname",
                    ["last name"] = "Surname",
                    ["dob"] = "DateOfBirth",
                    ["dateofbirth"] = "DateOfBirth",
                    ["gender"] = "Gender",
                    ["civilstatus"] = "CivilStatus",
                    ["civil status"] = "CivilStatus",
                    ["nationality"] = "Nationality",
                    ["religion"] = "Religion",
                    ["bloodtype"] = "BloodType",
                    ["blood type"] = "BloodType",

                    // Academic Information
                    ["course"] = "Course",
                    ["yearlevel"] = "YearLevel",
                    ["year level"] = "YearLevel",
                    ["section"] = "Section",
                    ["gpa"] = "GPA",
                    ["status"] = "Status",
                    ["scholarship"] = "Scholarship",
                    ["remarks"] = "Remarks",
                    ["studenttype"] = "StudentType",
                    ["student type"] = "StudentType",
                    ["lastenrolledsemester"] = "LastEnrolledSemester",
                    ["last enrolled semester"] = "LastEnrolledSemester",

                    // Contact Information
                    ["email"] = "Email",
                    ["phonenumber"] = "PhoneNumber",
                    ["phone number"] = "PhoneNumber",
                    ["address"] = "Address",

                    // Family / Guardian
                    ["guardianname"] = "GuardianName",
                    ["guardian name"] = "GuardianName",
                    ["guardiancontact"] = "GuardianContact",
                    ["guardian contact"] = "GuardianContact",
                    ["emergencycontact"] = "EmergencyContact",
                    ["emergency contact"] = "EmergencyContact",

                    // Dates
                    ["admissiondate"] = "AdmissionDate",
                    ["admission date"] = "AdmissionDate",
                    ["graduationdate"] = "GraduationDate",
                    ["graduation date"] = "GraduationDate"
                };

                await csv.ReadAsync();
                csv.ReadHeader();
                var csvHeaders = csv.HeaderRecord;
                var students = new List<StudentModel>();
                var studentPropertyCache = typeof(StudentModel).GetProperties().ToDictionary(p => p.Name, p => p);

                // iterate through each record
                while (await csv.ReadAsync())
                {
                    if (csvHeaders == null)
                    {
                        return Results.BadRequest(new { message = "CSV headers are missing" });
                    }

                    // create a new student object for each record
                    var student = new StudentModel();
                    var validationErrors = new List<string>();

                    // start mapping fields based on header names
                    foreach (var header in csvHeaders)
                    {
                        var normalizedHeader = header.Replace(" ", "").ToLower();
                        if (map.TryGetValue(normalizedHeader, out var propertyName))
                        {
                            if (studentPropertyCache.TryGetValue(propertyName, out var propertyInfo))
                            {
                                var headerValue = csv.GetField(header);

                                // only validate fields that require validation
                                if (propertyInfo.PropertyType == typeof(string))
                                {
                                    headerValue = headerValue?.Trim();

                                    if (propertyInfo.Name == "Name" || propertyInfo.Name == "Surname")
                                    {
                                        if (!string.IsNullOrWhiteSpace(headerValue) && Regex.IsMatch(headerValue, @"^[a-zA-Z\s'-]+$"))
                                        {
                                            propertyInfo.SetValue(student, headerValue);
                                        }
                                        else
                                        {
                                            validationErrors.Add($"{propertyName} cannot be empty and must contain only letters, spaces, apostrophes, or hyphens");
                                        }
                                    }
                                    else if (propertyInfo.Name == "StudentId")
                                    {
                                        if (long.TryParse(headerValue, out _) && !string.IsNullOrWhiteSpace(headerValue))
                                        {
                                            propertyInfo.SetValue(student, headerValue);
                                        }
                                        else
                                        {
                                            validationErrors.Add($"{propertyName} cannot be empty and must be numeric");
                                        }
                                    }
                                    else if (propertyInfo.Name == "Gender")
                                    {
                                        var val = headerValue?.ToLower();
                                        if (val == "male" || val == "female")
                                        {
                                            propertyInfo.SetValue(student, val);
                                        }
                                        else
                                        {
                                            validationErrors.Add($"{propertyName} must be 'male' or 'female'");
                                        }
                                    }
                                    else if (propertyInfo.Name == "PhoneNumber")
                                    {
                                        if (!string.IsNullOrWhiteSpace(headerValue) && long.TryParse(headerValue, out _) && headerValue.Length == 11 && headerValue.StartsWith("09"))
                                        {
                                            propertyInfo.SetValue(student, headerValue);
                                        }
                                        else if (!string.IsNullOrWhiteSpace(headerValue))
                                        {
                                            validationErrors.Add($"{propertyName} must be numeric, 11 digits, starting with '09'");
                                        }
                                    }
                                    else if (propertyInfo.Name == "Email")
                                    {
                                        if (!string.IsNullOrWhiteSpace(headerValue) && headerValue.Contains("@") && headerValue.Contains("."))
                                        {
                                            propertyInfo.SetValue(student, headerValue);
                                        }
                                        else
                                        {
                                            propertyInfo.SetValue(student, headerValue);
                                            validationErrors.Add($"{propertyName} is not valid");
                                        }
                                    }
                                    else if (propertyInfo.Name == "GPA")
                                    {
                                        if (float.TryParse(headerValue, out var gpaValue))
                                        {
                                            if (gpaValue >= 0.0f && gpaValue <= 4.0f)
                                            {
                                                propertyInfo.SetValue(student, headerValue);
                                            }
                                            else
                                            {
                                                validationErrors.Add($"{propertyName} must be between 0.0 and 4.0");
                                            }
                                        }
                                        else if (!string.IsNullOrWhiteSpace(headerValue))
                                        {
                                            validationErrors.Add($"{propertyName} must be a valid float number");
                                        }
                                    }
                                    else
                                    {
                                        // set all other string fields without validation
                                        propertyInfo.SetValue(student, headerValue);
                                    }
                                }
                                else if (propertyInfo.PropertyType == typeof(DateTime?))
                                {
                                    if (DateTime.TryParse(headerValue, out var dateValue))
                                    {
                                        propertyInfo.SetValue(student, dateValue);
                                    }
                                    else if (!string.IsNullOrWhiteSpace(headerValue))
                                    {
                                        validationErrors.Add($"{propertyName} must be a valid date");
                                    }
                                }
                            }
                            else
                            {
                                throw new Exception($"Property '{propertyName}' not found in StudentModel.");
                            }
                        }
                        else
                        {
                            return Results.BadRequest(new { message = $"Unrecognized header: {header}" });
                        }
                    }

                    if (validationErrors.Count > 0)
                    {
                        student.Errors = validationErrors;
                    }

                    students.Add(student);
                }

                return Results.Ok(new { message = "File processed successfully", results = students });
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