namespace MyAuthenticationBackend.Models;
public class EnableTFAResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? QRCodeImageBase64 { get; set; }
    public string? ManualEntryKey { get; set; }
}