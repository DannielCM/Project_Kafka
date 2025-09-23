namespace AuthenticationBackend.Endpoints;
public class LogInRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string CaptchaId { get; set; }
    public required string CaptchaText { get; set; }
}