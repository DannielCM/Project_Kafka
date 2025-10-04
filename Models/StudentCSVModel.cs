namespace MyAuthenticationBackend.Models;
public class StudentCSVModel
{
    public string StudentId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string Email { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string Course { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}