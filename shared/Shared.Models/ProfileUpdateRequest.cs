namespace UserService.Models
{
    public class ProfileUpdateRequest
    {
        public string? Username { get; set; }
        public string? ProfileDescription { get; set; }
        public string? Location { get; set; }
    }
}