using System.Text.Json.Serialization;

namespace notification_service.Models.Dtos
{
    public class PushSubscriptionDto
    {
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = string.Empty;
        
        [JsonPropertyName("expirationTime")]
        public DateTime? ExpirationTime { get; set; }
        
        [JsonPropertyName("keys")]
        public PushSubscriptionKeys Keys { get; set; } = new();
    }

    public class PushSubscriptionKeys
    {
        [JsonPropertyName("p256dh")]
        public string P256Dh { get; set; } = string.Empty;
        
        [JsonPropertyName("auth")]
        public string Auth { get; set; } = string.Empty;
    }
}