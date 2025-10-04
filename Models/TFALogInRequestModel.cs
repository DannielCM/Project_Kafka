namespace MyAuthenticationBackend.Models;
public class TFALogInRequest
{
    public int Id { get; set; }
    public string Token { get; set; } = "";
    public int AccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; } = false;
}