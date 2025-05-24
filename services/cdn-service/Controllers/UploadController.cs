using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CdnService.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UploadController> _logger;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };
        private const string StorageFolder = "storage";

        public UploadController(
            IConfiguration configuration, 
            IWebHostEnvironment environment,
            ILogger<UploadController> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        [Authorize]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileUploadResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            var requestId = Guid.NewGuid().ToString();
            _logger.LogInformation("[{RequestId}] Starting file upload", requestId);

            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("[{RequestId}] No file was uploaded", requestId);
                    return BadRequest(new { message = "No file uploaded." });
                }

                var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("[{RequestId}] Invalid file type: {FileName}", requestId, file.FileName);
                    return BadRequest(new { message = "Invalid file type. Allowed types: .jpg, .jpeg, .png" });
                }

                if (file.Length > MaxFileSize)
                {
                    _logger.LogWarning("[{RequestId}] File size {FileSize} exceeds limit of {MaxSize} bytes", 
                        requestId, file.Length, MaxFileSize);
                    return BadRequest(new { message = $"File size exceeds the limit of {MaxFileSize / (1024 * 1024)}MB." });
                }

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var storagePath = Path.Combine(_environment.ContentRootPath, StorageFolder);
                var filePath = Path.Combine(storagePath, uniqueFileName);

                _logger.LogInformation("[{RequestId}] Saving file {FileName} as {UniqueName}", 
                    requestId, file.FileName, uniqueFileName);

                try
                {
                    Directory.CreateDirectory(storagePath);
                    await using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{RequestId}] Error saving file {FileName}", requestId, file.FileName);
                    throw new InvalidOperationException("An error occurred while saving the file.", ex);
                }

                var baseUrl = _configuration["Cdn:BaseUrl"] ?? string.Empty;
                var fileUrl = $"{baseUrl.TrimEnd('/')}/u/{uniqueFileName}";

                _logger.LogInformation("[{RequestId}] File uploaded successfully: {FileUrl}", requestId, fileUrl);
                return Ok(new FileUploadResponse { FileUrl = fileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Unhandled exception in UploadFile", requestId);
                throw; // Will be handled by the exception handling middleware
            }
        }

        [HttpDelete("{filename}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult DeleteFile(string filename)
        {
            var requestId = Guid.NewGuid().ToString();
            _logger.LogInformation("[{RequestId}] Deleting file: {FileName}", requestId, filename);

            try
            {
                if (string.IsNullOrWhiteSpace(filename))
                {
                    _logger.LogWarning("[{RequestId}] Empty filename provided", requestId);
                    return BadRequest(new { message = "Filename cannot be empty." });
                }

                var storagePath = Path.Combine(_environment.ContentRootPath, StorageFolder);
                var filePath = Path.Combine(storagePath, filename);

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("[{RequestId}] File not found: {FileName}", requestId, filename);
                    return NotFound(new { message = "File not found." });
                }

                try
                {
                    _logger.LogDebug("[{RequestId}] Attempting to delete file: {FilePath}", requestId, filePath);
                    System.IO.File.Delete(filePath);
                    _logger.LogInformation("[{RequestId}] Successfully deleted file: {FileName}", requestId, filename);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{RequestId}] Error deleting file {FileName}", requestId, filename);
                    throw new InvalidOperationException("An error occurred while deleting the file.", ex);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Unhandled exception in DeleteFile", requestId);
                throw; // Will be handled by the exception handling middleware
            }
        }
    }

    public class FileUploadResponse
    {
        [Required]
        public string FileUrl { get; set; }
    }
} 