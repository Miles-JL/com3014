using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Shared.Models;
using Shared.Auth;
using UserService.Data;
using UserService.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<UserController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly UserSyncService _syncService;

        public UserController(
            AppDbContext db, 
            ILogger<UserController> logger, 
            IWebHostEnvironment env,
            UserSyncService syncService)
        {
            _db = db;
            _logger = logger;
            _env = env;
            _syncService = syncService;
        }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<User>> GetProfile()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
            {
                _logger.LogWarning("Invalid user ID in token");
                return Unauthorized("Invalid user ID claim");
            }

            var username = User.Identity?.Name;
            _logger.LogInformation("Getting profile for user ID: {UserId}, Username: {Username}", id, username);

            var user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", id);
                return NotFound();
            }

            user.PasswordHash = string.Empty; // Don't return password hash
            
            // Ensure profile image URL is properly formatted
            if (!string.IsNullOrEmpty(user.ProfileImage) && !user.ProfileImage.StartsWith("http"))
            {
                var httpRequest = HttpContext.Request;
                var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host.Value}";
                user.ProfileImage = $"{baseUrl}{user.ProfileImage}";
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile");
            return StatusCode(500, "An error occurred while retrieving the profile");
        }
    }

    [HttpGet("search")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<UserSearchResult>>> SearchUsers(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            return BadRequest("Search query must be at least 3 characters");
        }

        var results = await _db.Users
            .Where(u => u.Username.Contains(query))
            .Select(u => new UserSearchResult
            {
                Id = u.Id,
                Username = u.Username,
                ProfileImage = u.ProfileImage,
                ProfileDescription = u.ProfileDescription
            })
            .Take(10)
            .ToListAsync();

        return Ok(results);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UserService.Models.ProfileUpdateRequest profileRequest)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
            {
                _logger.LogWarning("Invalid user ID in token");
                return Unauthorized("Invalid user ID claim");
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found for update: {UserId}", id);
                return NotFound();
            }

            // Update username if provided and unique
            if (!string.IsNullOrEmpty(profileRequest.Username) && profileRequest.Username != user.Username)
            {
                if (await _db.Users.AnyAsync(u => u.Username == profileRequest.Username))
                {
                    return BadRequest(new { message = "Username is already taken" });
                }
                
                user.Username = profileRequest.Username;
                _logger.LogInformation("Username updated for user {UserId}: {NewUsername}", id, profileRequest.Username);
            }

            // Update other fields if provided
            if (profileRequest.ProfileDescription != null)
            {
                user.ProfileDescription = profileRequest.ProfileDescription;
            }
            
            if (profileRequest.Location != null)
            {
                user.Location = profileRequest.Location;
            }

            user.LastUpdated = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var response = new 
            {
                id = user.Id,
                username = user.Username,
                profileDescription = user.ProfileDescription,
                location = user.Location,
                profileImage = user.ProfileImage,
                lastUpdated = user.LastUpdated
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, "An error occurred while updating the profile");
        }
    }

    [HttpPost("profile-image")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfileImage([FromForm] UploadProfileImageRequest imageRequest)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
            {
                _logger.LogWarning("Invalid user ID in token");
                return Unauthorized("Invalid user ID claim");
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found for image upload: {UserId}", id);
                return NotFound();
            }

            var file = imageRequest.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                return BadRequest("Invalid file type. Only JPEG, PNG, and GIF are allowed.");
            }

            // Create directory if it doesn't exist
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate unique filename
            var fileExtension = Path.GetExtension(file.FileName);
            var fileName = $"{id}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Delete old profile image if it exists
            if (!string.IsNullOrEmpty(user.ProfileImage) && user.ProfileImage.StartsWith("/uploads/"))
            {
                var oldFilePath = Path.Combine(_env.WebRootPath, user.ProfileImage.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    try
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old profile image");
                    }
                }
            }

            // Save new file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Update user profile with new image path
            user.ProfileImage = $"/uploads/{fileName}";
            user.LastUpdated = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Profile image updated for user {UserId}", id);

            // Return absolute URL for the image
            var httpRequest = HttpContext.Request;
            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host.Value}";
            
            return Ok(new { 
                profileImage = $"{baseUrl}{user.ProfileImage}",
                message = "Profile image uploaded successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile image");
            return StatusCode(500, "An error occurred while uploading the profile image");
        }
    }

    [HttpPost("sync")]
    [AllowAnonymous] // This endpoint needs to be called from auth-service without auth
    public async Task<IActionResult> SyncUser([FromBody] UserSyncRequest syncRequest)
    {
        try
        {
            // Check if the user already exists
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Id == syncRequest.Id);
            
            if (existing != null)
            {
                // Update existing user with any new information
                existing.Username = syncRequest.Username;
                
                if (!string.IsNullOrEmpty(syncRequest.ProfileImage))
                {
                    existing.ProfileImage = syncRequest.ProfileImage;
                }
                
                if (!string.IsNullOrEmpty(syncRequest.ProfileDescription))
                {
                    existing.ProfileDescription = syncRequest.ProfileDescription;
                }
                
                if (!string.IsNullOrEmpty(syncRequest.Location))
                {
                    existing.Location = syncRequest.Location;
                }
                
                existing.LastUpdated = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                
                _logger.LogInformation("Updated existing user during sync: {UserId}", syncRequest.Id);
                return Ok();
            }

            // Create new user
            var user = new User
            {
                Id = syncRequest.Id,
                Username = syncRequest.Username,
                ProfileImage = syncRequest.ProfileImage ?? string.Empty,
                ProfileDescription = syncRequest.ProfileDescription ?? string.Empty,
                Location = syncRequest.Location ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Created new user during sync: {UserId}", syncRequest.Id);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user");
            return StatusCode(500, "An error occurred while syncing the user");
        }
    }
    
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<UserProfile>> GetUserById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Format profile image URL if needed
        var profileImage = user.ProfileImage;
        if (!string.IsNullOrEmpty(profileImage) && !profileImage.StartsWith("http"))
        {
            var httpRequest = HttpContext.Request;
            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host.Value}";
            profileImage = $"{baseUrl}{profileImage}";
        }

        return new UserProfile
        {
            Id = user.Id,
            Username = user.Username,
            ProfileImage = profileImage,
            ProfileDescription = user.ProfileDescription,
            Location = user.Location,
            CreatedAt = user.CreatedAt
        };
    }
}};