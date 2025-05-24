using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CdnService.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LocalFileStorageService> _logger;
        private readonly string _basePath;
        private readonly string _baseUrl;

        public LocalFileStorageService(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<LocalFileStorageService> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            
            // Ensure storage directory exists
            _basePath = Path.Combine(_environment.ContentRootPath, "storage");
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
            
            _baseUrl = _configuration["Cdn:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5250";
        }

        public async Task<string> UploadFileAsync(IFormFile file, string? oldFileName = null)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is empty", nameof(file));
            }

            // Generate a unique filename
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(_basePath, uniqueFileName);

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved: {FilePath}", filePath);

            // Delete old file if specified
            if (!string.IsNullOrEmpty(oldFileName))
            {
                await DeleteFileAsync(oldFileName);
            }

            return uniqueFileName;
        }

        public async Task<bool> DeleteFileAsync(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return false;
            }

            var filePath = Path.Combine(_basePath, filename);
            
            // Check if file exists asynchronously
            if (!await Task.Run(() => File.Exists(filePath)))
            {
                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                return false;
            }

            try
            {
                // Perform deletion asynchronously
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("File deleted: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                return false;
            }
        }

        public string GetFileUrl(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return string.Empty;
            }
            
            return $"{_baseUrl}/u/{Uri.EscapeDataString(filename)}";
        }
    }
}
