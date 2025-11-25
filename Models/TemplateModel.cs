namespace MyAuthenticationBackend.Models;
public class Template
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = "";
    public Dictionary<string, int> Map { get; set; } = new Dictionary<string, int>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}