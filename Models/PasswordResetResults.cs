namespace MyAuthenticationBackend.Models
{
    public class PasswordResetResults
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }
}