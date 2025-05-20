using Microsoft.AspNetCore.Http;

namespace UserService.Models
{
    public class UploadProfileImageRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}