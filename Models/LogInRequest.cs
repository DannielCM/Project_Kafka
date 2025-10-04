namespace MyAuthenticationBackend.Models;
public class LogInRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string CaptchaId { get; set; } = "";
    public string CaptchaText { get; set; } = "";
}