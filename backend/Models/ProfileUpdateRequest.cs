namespace JwtAuthApi.Models;

public class ProfileUpdateRequest
{
    public string? Username { get; set; }
    public string? ProfileDescription { get; set; }
}