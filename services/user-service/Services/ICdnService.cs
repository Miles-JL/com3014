using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace UserService.Services
{
    public interface ICdnService
    {
        Task<string?> UploadProfileImageAsync(IFormFile file, string accessToken);
    }
}
