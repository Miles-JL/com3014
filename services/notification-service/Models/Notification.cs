using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace notification_service.Models;

public class Notification
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public string? Url { get; set; }
    
    public bool IsRead { get; set; } = false;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Additional metadata as JSON string for flexibility
    public string? Metadata { get; set; }
}
