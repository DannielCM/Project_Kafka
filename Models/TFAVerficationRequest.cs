namespace AuthenticationBackend.Endpoints;
public class TFAVerificationRequest
{
    public string Token { get; set; } = "";
    public string Code { get; set; } = "";
}