using System;

namespace UserService.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
        public string ProfileDescription { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}