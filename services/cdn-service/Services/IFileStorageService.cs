using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace CdnService.Services
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string? oldFileName = null);
        Task<bool> DeleteFileAsync(string filename);
        string GetFileUrl(string filename);
    }
}
