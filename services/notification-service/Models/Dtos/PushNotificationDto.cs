using System.ComponentModel.DataAnnotations;

namespace notification_service.Models.Dtos
{
    public class PushNotificationDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Body { get; set; } = string.Empty;
        
        public string Url { get; set; } = string.Empty;
    }
}
