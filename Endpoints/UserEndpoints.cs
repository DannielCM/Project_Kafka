using MyAuthenticationBackend.Models;
using CsvHelper;
using CsvHelper.TypeConversion;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using BackendAuthentication;
using Microsoft.AspNetCore.Authorization;

namespace AuthenticationBackend.Endpoints;
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/user").DisableAntiforgery();

        group.MapPost("/csv/student/upload", [Authorize] async (IFormFile file, DbHelper dbHelper) =>
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

                // Check StudentId is numeric
                if (string.IsNullOrWhiteSpace(record.StudentId))
                {
                    validationErrors.Add("StudentId is missing");
                }
                else if (!int.TryParse(record.StudentId, out _))
                {
                    validationErrors.Add("StudentId must be numeric");
                }

                // Other validations
                if (string.IsNullOrWhiteSpace(record.Name)) { validationErrors.Add("FirstName is missing"); }
                if (string.IsNullOrWhiteSpace(record.Surname)) { validationErrors.Add("LastName is missing"); }


                if (string.IsNullOrWhiteSpace(record.DateOfBirth) || !DateTime.TryParseExact(record.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    validationErrors.Add("DateOfBirth is missing or invalid");
                }
                if (string.IsNullOrWhiteSpace(record.Course)) { validationErrors.Add("Course is missing"); }

                // Add to success or failure
                if (validationErrors.Count > 0)
                {
                    failureList.Add(new StudentCSVModel
                    {
                        StudentId = record.StudentId,
                        Name = record.Name,
                        Surname = record.Surname,
                        MiddleName = record.MiddleName,
                        DateOfBirth = record.DateOfBirth,
                        Course = record.Course,
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

                var result = dbHelper.BulkInsertStudents(successList, connection);

                insertResults.AddRange(result.inserted);

                result.skipped.ForEach(s => failureList.Add(new StudentCSVModel
                {
                    StudentId = s.StudentId,
                    Name = s.Name,
                    Surname = s.Surname,
                    MiddleName = s.MiddleName,
                    DateOfBirth = s.DateOfBirth,
                    Course = s.Course,
                    Errors = new List<string> { "Duplicate StudentId" }
                }));
            }

            return Results.Ok(new
            {
                success = insertResults,
                failures = failureList
            });
        });
    }
}