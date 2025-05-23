using System.Threading.Tasks;

namespace UserService.Services
{
    public class UserProfileUpdateDto
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string? ProfileImageUrl { get; set; }
        // Add any other fields that auth-service might expect or need for an update
    }

    public interface IAuthSyncService
    {
        Task<bool> UpdateUserProfileAsync(UserProfileUpdateDto userProfileUpdate, string accessToken);
    }
}
