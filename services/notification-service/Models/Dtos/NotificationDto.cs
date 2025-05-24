using System.Text.Json.Serialization;

namespace notification_service.Models.Dtos;

public class NotificationDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
    
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "/logo192.png";
    
    [JsonIgnore]
    public bool IsRead { get; set; }
    
    [JsonIgnore]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
