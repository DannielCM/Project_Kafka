namespace MyAuthenticationBackend.Models;
public class Account
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string StoredHashedPassword { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? TwoFactorSecret { get; set; }
}