namespace Shared.Models;

public class User
{
    public int Id { get; set; }  // PK
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ProfileImage { get; set; } = string.Empty;
    public string ProfileDescription { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsAdmin { get; set; }
}
