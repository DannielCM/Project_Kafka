namespace MyAuthenticationBackend.Models;
public class AuditEvent
{
    public int UserId { get; set; }
    public string Action { get; set; }
    public string ResourceType { get; set; }
    public string ResourceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Status { get; set; }
    public string Details { get; set; }
}
