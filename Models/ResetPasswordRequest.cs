namespace AuthenticationBackend.Endpoints;
public class ResetPasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
    public required string NewPasswordConfirmation { get; set; }
}