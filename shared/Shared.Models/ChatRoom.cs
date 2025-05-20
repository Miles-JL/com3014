using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models;

public class ChatRoom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CreatorId { get; set; }
    
    [ForeignKey("CreatorId")]
    public User Creator { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}