namespace UserService.Models
{
    public class UserSearchResult
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
        public string ProfileDescription { get; set; } = string.Empty;
    }
}