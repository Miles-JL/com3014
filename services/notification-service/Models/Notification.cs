using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace notification_service.Models;

public class Notification
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    public int RecipientId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // e.g., "dm", "friend_request"
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public bool IsRead { get; set; } = false;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Additional metadata as JSON string for flexibility
    public string? Metadata { get; set; }
}
