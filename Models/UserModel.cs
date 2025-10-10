namespace MyAuthenticationBackend.Models;
public class User
{
    public int AccountId { get; set; }  
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}