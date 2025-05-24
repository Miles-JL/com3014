using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CdnService.Services;

namespace CdnService.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly ILogger<UploadController> _logger;
        private readonly IFileStorageService _fileStorageService;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        public UploadController(
            ILogger<UploadController> logger,
            IFileStorageService fileStorageService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
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

                _logger.LogInformation("[{RequestId}] Uploading file: {FileName}", requestId, file.FileName);

                try
                {
                    // Upload the file using the storage service
                    var fileName = await _fileStorageService.UploadFileAsync(file);
                    var fileUrl = _fileStorageService.GetFileUrl(fileName);

                    _logger.LogInformation("[{RequestId}] File uploaded successfully: {FileUrl}", requestId, fileUrl);
                    return Ok(new FileUploadResponse { FileUrl = fileUrl });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{RequestId}] Error saving file {FileName}", requestId, file.FileName);
                    throw new InvalidOperationException("An error occurred while saving the file.", ex);
                }
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
        public async Task<IActionResult> DeleteFile(string filename)
        {
            var requestId = Guid.NewGuid().ToString();
            _logger.LogInformation("[{RequestId}] Deleting file: {FileName}", requestId, filename);

            try
            {
                if (string.IsNullOrEmpty(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    _logger.LogWarning("[{RequestId}] Invalid filename: {FileName}", requestId, filename);
                    return BadRequest(new { message = "Invalid filename." });
                }

                var deleted = await _fileStorageService.DeleteFileAsync(filename);
                if (!deleted)
                {
                    _logger.LogWarning("[{RequestId}] File not found: {FileName}", requestId, filename);
                    return NotFound(new { message = "File not found." });
                }

                _logger.LogInformation("[{RequestId}] File deleted: {FileName}", requestId, filename);
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
        public string FileUrl { get; set; } = string.Empty;

        public FileUploadResponse() { }
        
        public FileUploadResponse(string fileUrl)
        {
            FileUrl = fileUrl;
        }
    }
} 