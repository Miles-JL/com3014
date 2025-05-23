using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CdnService.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        public UploadController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        [HttpPost]
        [Authorize]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileUploadResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid file type. Allowed types: .jpg, .jpeg, .png" });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new { message = $"File size exceeds the limit of {MaxFileSize / (1024 * 1024)}MB." });
            }

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var storagePath = Path.Combine(_environment.ContentRootPath, "storage");
            var filePath = Path.Combine(storagePath, uniqueFileName);

            try
            {
                await using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }
            catch (Exception ex)
            {
                // Log the exception (not shown here for brevity)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while saving the file." });
            }

            var baseUrl = _configuration["Cdn:BaseUrl"];
            var fileUrl = $"{baseUrl}/u/{uniqueFileName}";

            return Ok(new FileUploadResponse { FileUrl = fileUrl });
        }

        [HttpDelete("{filename}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DeleteFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return BadRequest(new { message = "Filename cannot be empty." });
            }

            var storagePath = Path.Combine(_environment.ContentRootPath, "storage");
            var filePath = Path.Combine(storagePath, filename);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "File not found." });
            }

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                // Log the exception (not shown here for brevity)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the file." });
            }

            return NoContent();
        }
    }

    public class FileUploadResponse
    {
        [Required]
        public string FileUrl { get; set; }
    }
} 