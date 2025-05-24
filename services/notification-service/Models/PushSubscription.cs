using System.ComponentModel.DataAnnotations;

namespace notification_service.Models
{
    public class PushSubscription
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string Endpoint { get; set; } = string.Empty;
        
        [Required]
        public string P256Dh { get; set; } = string.Empty;
        
        [Required]
        public string Auth { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}