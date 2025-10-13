namespace MyAuthenticationBackend.Models;
public class TFAVerificationResult
{
	public bool IsSuccess { get; set; } = false;
	public string Message { get; set; } = string.Empty;
	public Account Account { get; set; } = new Account();
}