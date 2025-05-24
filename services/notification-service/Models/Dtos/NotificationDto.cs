using System.Text.Json.Serialization;

namespace notification_service.Models.Dtos;

public class NotificationDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }
}
