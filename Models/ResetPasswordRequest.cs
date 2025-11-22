namespace MyAuthenticationBackend.Models;
public class ResetPasswordRequestModel
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? NewPasswordConfirmation { get; set; }
}