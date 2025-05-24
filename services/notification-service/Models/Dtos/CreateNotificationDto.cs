using System.Text.Json.Serialization;

namespace notification_service.Models.Dtos;

public class CreateNotificationDto
{
    [JsonPropertyName("recipientId")]
    public int RecipientId { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }
}
