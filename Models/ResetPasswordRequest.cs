namespace AuthenticationBackend.Endpoints;
public class ResetPasswordRequest
{
    public string? CurrentPassword { get; set; } = "";
    public string? NewPassword { get; set; } = "";
    public string? NewPasswordConfirmation { get; set; } = "";
}