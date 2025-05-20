using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatroomService.Data;
using Shared.Models;
using System;
using System.Threading.Tasks;

namespace ChatroomService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<UserController> _logger;

        public UserController(AppDbContext db, ILogger<UserController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpPost("sync")]
        [AllowAnonymous] // This endpoint is called from user-service
        public async Task<IActionResult> SyncUser([FromBody] UserSyncRequest request)
        {
            try
            {
                // Check if the user already exists
                var existing = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.Id);
                
                if (existing != null)
                {
                    // Update existing user with any new information
                    existing.Username = request.Username;
                    
                    if (!string.IsNullOrEmpty(request.ProfileImage))
                    {
                        existing.ProfileImage = request.ProfileImage;
                    }
                    
                    if (!string.IsNullOrEmpty(request.ProfileDescription))
                    {
                        existing.ProfileDescription = request.ProfileDescription;
                    }
                    
                    if (!string.IsNullOrEmpty(request.Location))
                    {
                        existing.Location = request.Location;
                    }
                    
                    await _db.SaveChangesAsync();
                    
                    _logger.LogInformation("Updated existing user during sync: {UserId}", request.Id);
                    return Ok();
                }

                // Create new user
                var user = new User
                {
                    Id = request.Id,
                    Username = request.Username,
                    ProfileImage = request.ProfileImage ?? string.Empty,
                    ProfileDescription = request.ProfileDescription ?? string.Empty,
                    Location = request.Location ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                
                _logger.LogInformation("Created new user during sync: {UserId}", request.Id);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user");
                return StatusCode(500, "An error occurred while syncing the user");
            }
        }
    }
}