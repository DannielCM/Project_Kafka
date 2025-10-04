namespace MyAuthenticationBackend.Models;
public class CaptchaValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
