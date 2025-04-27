namespace JwtAuthApi.Models;

public class User
{
    public int Id { get; set; }  // PK
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
