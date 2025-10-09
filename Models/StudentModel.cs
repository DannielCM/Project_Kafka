namespace MyAuthenticationBackend.Models
{
    public class StudentModel
    {
        // Required / essential
        public string StudentId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Surname { get; set; } = "";
        public string DateOfBirth { get; set; } = "";
        public string Course { get; set; } = "";
        public string Gender { get; set; } = "";
        public string CivilStatus { get; set; } = "";

        // Optional / commonly included (that exist in DB)
        public string MiddleName { get; set; } = "";
        public string YearLevel { get; set; } = "";
        public string Section { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Address { get; set; } = "";
        public string Nationality { get; set; } = "";
        public string Religion { get; set; } = "";
        public string GuardianName { get; set; } = "";
        public string GuardianContact { get; set; } = "";
        public string EmergencyContact { get; set; } = "";
        public string AdmissionDate { get; set; } = "";
        public string GraduationDate { get; set; } = "";
        public string GPA { get; set; } = "";
        public string Status { get; set; } = "";
        public string Scholarship { get; set; } = "";
        public string Remarks { get; set; } = "";
        public string StudentType { get; set; } = "";
    }
}
