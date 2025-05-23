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
using UserService.Services; 
using Microsoft.AspNetCore.Authentication; 

// DTO for internal user details
public class UserInternalDto
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string ProfileImage { get; set; }
}

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
        private readonly ICdnService _cdnService; 
        private readonly IAuthSyncService _authSyncService; 

        public UserController(
            AppDbContext db,
            ILogger<UserController> logger,
            IWebHostEnvironment env,
            UserSyncService syncService,
            ICdnService cdnService,             
            IAuthSyncService authSyncService)    
        {
            _db = db;
            _logger = logger;
            _env = env;
            _syncService = syncService;
            _cdnService = cdnService;           
            _authSyncService = authSyncService; 
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
                // First check if username is available in the user service
                if (await _db.Users.AnyAsync(u => u.Username == profileRequest.Username && u.Id != id))
                {
                    return BadRequest(new { message = "Username is already taken" });
                }

                // Call Auth Service to update the username
                try
                {
                    using var httpClient = new HttpClient();
                    var authResponse = await httpClient.PostAsJsonAsync(
                        "http://localhost:5106/api/Auth/update-username", 
                        new { NewUsername = profileRequest.Username });

                    if (!authResponse.IsSuccessStatusCode)
                    {
                        var error = await authResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Failed to update username in Auth Service: {Error}", error);
                        return StatusCode(500, "Failed to update username. Please try again later.");
                    }

                    // If auth service update was successful, update the local username
                    user.Username = profileRequest.Username;
                    _logger.LogInformation("Username updated for user {UserId}: {NewUsername}", id, profileRequest.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling Auth Service to update username");
                    return StatusCode(500, "Failed to update username due to an internal error.");
                }
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
    public async Task<IActionResult> UploadProfileImage(IFormFile file)
    {
        _logger.LogInformation("Attempting to upload profile image for user.");

        // 1. Get User Info & Token
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId) || string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("Invalid or missing user claims.");
            return Unauthorized("Invalid user claims. Please re-authenticate.");
        }

        var accessToken = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token not found for user {UserId}.", userId);
            return Unauthorized("Access token not found. Please re-authenticate.");
        }

        _logger.LogInformation("User ID: {UserId}, Username: {Username} attempting image upload.", userId, username);

        // 2. File Validation
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        if (file.Length > maxFileSize)
        {
            return BadRequest(new { message = $"File size exceeds the limit of {maxFileSize / (1024 * 1024)}MB." });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Invalid file type. Allowed types: .jpg, .jpeg, .png" });
        }

        try
        {
            // 0. Get current user to find old profile image URL (if any)
            var localUserForOldImage = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            string? oldProfileImageFileName = null;
            if (localUserForOldImage != null && !string.IsNullOrEmpty(localUserForOldImage.ProfileImage))
            {
                // Extract just the filename from the URL. Assumes URL like http://cdn.myapp.com/u/filename.jpg
                // or /uploads/filename.jpg if it was a local path previously
                oldProfileImageFileName = Path.GetFileName(localUserForOldImage.ProfileImage);
                _logger.LogInformation("Old profile image found for user {UserId}: {OldProfileImageFileName}", userId, oldProfileImageFileName);
            }

            // 3. Call CDN Service
            _logger.LogInformation("Calling CDN service to upload image for user {UserId}.", userId);
            var cdnFileUrl = await _cdnService.UploadProfileImageAsync(file, accessToken);
            if (string.IsNullOrEmpty(cdnFileUrl))
            {
                _logger.LogError("CDN service failed to return a file URL for user {UserId}.", userId);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Error uploading image to CDN. The CDN service did not return a valid URL." });
            }
            _logger.LogInformation("Image uploaded to CDN for user {UserId}. URL: {CdnFileUrl}", userId, cdnFileUrl);

            // 4. Call Auth Sync Service
            _logger.LogInformation("Calling Auth Sync service to update profile for user {UserId}.", userId);
            var userProfileUpdateDto = new UserProfileUpdateDto
            {
                UserId = userId.ToString(), // Though auth-service gets it from token, good to have for consistency
                Username = username, // Send current username, auth-service can decide if it wants to use it
                ProfileImageUrl = cdnFileUrl
            };
            var authUpdateSuccess = await _authSyncService.UpdateUserProfileAsync(userProfileUpdateDto, accessToken);
            if (!authUpdateSuccess)
            {
                _logger.LogError("Auth Sync service failed to update profile for user {UserId} with URL {CdnFileUrl}.", userId, cdnFileUrl);
                // Decide if this is a critical failure. For now, let's assume it is, but could be a soft fail.
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Error updating user profile in authentication service after CDN upload." });
            }
            _logger.LogInformation("Profile updated in Auth Sync service for user {UserId}.", userId);

            // 5. Update Local User Database
            var localUser = await _db.Users.FindAsync(userId);
            if (localUser != null)
            {
                localUser.ProfileImage = cdnFileUrl; // Update with new URL
                localUser.LastUpdated = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Local user profile image updated for user {UserId}.", userId);

                // 5a. Delete old image from CDN if it existed and all updates were successful
                if (!string.IsNullOrEmpty(oldProfileImageFileName) && oldProfileImageFileName != Path.GetFileName(cdnFileUrl)) // Don't delete if it's somehow the same file
                {
                    _logger.LogInformation("Attempting to delete old profile image {OldProfileImageFileName} from CDN for user {UserId}.", oldProfileImageFileName, userId);
                    bool deleteSuccess = await _cdnService.DeleteProfileImageAsync(oldProfileImageFileName, accessToken);
                    if (deleteSuccess)
                    {
                        _logger.LogInformation("Successfully deleted old profile image {OldProfileImageFileName} from CDN for user {UserId}.", oldProfileImageFileName, userId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete old profile image {OldProfileImageFileName} from CDN for user {UserId}. This might require manual cleanup.", oldProfileImageFileName, userId);
                        // Not returning an error to the client for this, as the main upload was successful.
                    }
                }
            }
            else
            {
                _logger.LogWarning("Local user with ID {UserId} not found for profile image update. Auth service was updated.", userId);
                // Not necessarily an error if auth service is the source of truth and user-service is a cache/replica
            }

            // 6. Return Result
            return Ok(new { fileUrl = cdnFileUrl, message = "Profile image uploaded and updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during profile image upload for user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred. Please try again later." });
        }
    }

    [HttpPost("sync-with-auth")]
    [Authorize]
    public async Task<IActionResult> SyncWithAuthService()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
            {
                _logger.LogWarning("Invalid user ID in token during sync");
                return Unauthorized("Invalid user ID claim");
            }

            var user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User not found during sync: {UserId}", id);
                return NotFound("User not found");
            }

            // Call Auth Service to update the user data
            try
            {
                using var httpClient = new HttpClient();
                var authResponse = await httpClient.PostAsJsonAsync(
                    "http://localhost:5106/api/Auth/update-user", 
                    new 
                    {
                        Id = user.Id,
                        Username = user.Username,
                        ProfileImage = user.ProfileImage,
                        ProfileDescription = user.ProfileDescription,
                        Location = user.Location
                    });

                if (!authResponse.IsSuccessStatusCode)
                {
                    var error = await authResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to sync with Auth Service: {Error}", error);
                    return StatusCode(500, "Failed to sync with authentication service");
                }

                return Ok("User data synced with auth service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing with Auth Service");
                return StatusCode(500, "Failed to sync with authentication service");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SyncWithAuthService");
            return StatusCode(500, "An error occurred while syncing with auth service");
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

        // New Internal Endpoint for service-to-service communication
        [HttpGet("internal/{id:int}")]
        [AllowAnonymous] // Consider a more secure mechanism for production
        public async Task<ActionResult<UserInternalDto>> GetInternalUserDetails(int id)
        {
            _logger.LogInformation("Internal request for user details for ID: {UserId}", id);

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            { 
                _logger.LogWarning("Internal request: User not found: {UserId}", id);
                return NotFound(new { Message = $"User with ID {id} not found" });
            }

            return Ok(new UserInternalDto
            {
                Id = user.Id,
                Username = user.Username,
                ProfileImage = user.ProfileImage
            });
        }
    }
};