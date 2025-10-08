namespace MyAuthenticationBackend.Models;
public class Account
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string StoredHashedPassword { get; set; } = "";
    public string Role { get; set; } = "";
    public string? TwoFactorSecret { get; set; } = "";
}