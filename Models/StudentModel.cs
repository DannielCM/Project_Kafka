namespace MyAuthenticationBackend.Models;
public class StudentModel
{
    // Primary Identifier
    public string StudentId { get; set; } = string.Empty;

    // Personal Information
    public string Name { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string CivilStatus { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string Religion { get; set; } = string.Empty;
    public string BloodType { get; set; } = string.Empty;

    // Academic Information
    public string Course { get; set; } = string.Empty;
    public string YearLevel { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string GPA { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Scholarship { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string StudentType { get; set; } = string.Empty;
    public string LastEnrolledSemester { get; set; } = string.Empty;

    // Contact Information
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // Family / Guardian
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianContact { get; set; } = string.Empty;
    public string EmergencyContact { get; set; } = string.Empty;

    // Dates
    public DateTime? AdmissionDate { get; set; }
    public DateTime? GraduationDate { get; set; }

    // Validation Errors
    public List<string> Errors { get; set; } = new List<string>();
}
