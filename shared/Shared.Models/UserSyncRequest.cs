namespace Shared.Models;

public class UserSyncRequest
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfileImage { get; set; }
    public string? ProfileDescription { get; set; }
    public string? Location { get; set; }
    public bool IsAdmin { get; set; }
}
