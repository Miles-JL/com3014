using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using Shared.Auth;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System;
using System.Security.Claims;
using AuthService.Data;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly JwtService _jwt;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext db, JwtService jwt, ILogger<AuthController> logger)
        {
            _db = db;
            _jwt = jwt;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            if (await _db.Users.AnyAsync(u => u.Username == user.Username))
                return BadRequest("User already exists");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Sync to user-service (mandatory)
            using var httpClient = new HttpClient();
            var syncPayload = new UserSyncRequest
            {
                Id = user.Id,
                Username = user.Username,
                ProfileImage = user.ProfileImage,
                ProfileDescription = user.ProfileDescription,
                Location = user.Location
            };

            // TODO: Dockerize user-service and use its container name instead of localhost (when we intend to dockerize microservices)
            var syncResponse = await httpClient.PostAsJsonAsync("http://localhost:5117/api/User/sync", syncPayload);
            if (!syncResponse.IsSuccessStatusCode)
            {
                return StatusCode(500, "Failed to sync user profile to user-service");
            }

            return Ok("User registered");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User credentials)
        {
            // Special case for admin login
            if (credentials.Username == "admin" && credentials.PasswordHash == "admin")
            {
                var adminUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                if (adminUser == null)
                {
                    // Create admin user if it doesn't exist
                    adminUser = new User
                    {
                        Username = "admin",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                        IsAdmin = true
                    };
                    _db.Users.Add(adminUser);
                    await _db.SaveChangesAsync();
                    
                    // Sync admin to user-service
                    try 
                    {
                        using var httpClient = new HttpClient();
                        var syncPayload = new UserSyncRequest
                        {
                            Id = adminUser.Id,
                            Username = adminUser.Username,
                            IsAdmin = true
                        };
                        await httpClient.PostAsJsonAsync("http://localhost:5117/api/User/sync", syncPayload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing admin user");
                    }
                }
                
                var adminToken = _jwt.GenerateToken(adminUser);
                return Ok(new { token = adminToken });
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == credentials.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(credentials.PasswordHash, user.PasswordHash))
                return Unauthorized();

            var token = _jwt.GenerateToken(user);

            // Sync to user-service (optional, soft fail)
            try
            {
                using var httpClient = new HttpClient();
                var syncPayload = new UserSyncRequest
                {
                    Id = user.Id,
                    Username = user.Username,
                    ProfileImage = user.ProfileImage,
                    ProfileDescription = user.ProfileDescription,
                    Location = user.Location
                };

                // TODO: Dockerize user-service and use its container name instead of localhost (when we intend to dockerize microservices)
                await httpClient.PostAsJsonAsync("http://localhost:5117/api/User/sync", syncPayload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to sync user during login. Continuing anyway.");
            }

            return Ok(new { token });
        }

        [HttpPost("update-user")]
        [Authorize]
        public async Task<IActionResult> UpdateUser([FromBody] UserUpdateRequest request)
        {
            try
            {
                // Get user ID from the JWT token claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized("Invalid user ID claim");
                }

                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }


                // Update user properties if they are provided in the request
                if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
                {
                    if (await _db.Users.AnyAsync(u => u.Username == request.Username && u.Id != userId))
                    {
                        return BadRequest("Username is already taken");
                    }
                    user.Username = request.Username;
                }

                if (request.ProfileImage != null)
                {
                    user.ProfileImage = request.ProfileImage;
                }

                if (request.ProfileDescription != null)
                {
                    user.ProfileDescription = request.ProfileDescription;
                }

                if (request.Location != null)
                {
                    user.Location = request.Location;
                }

                await _db.SaveChangesAsync();

                // Sync with User Service
                try
                {
                    var syncPayload = new
                    {
                        Id = user.Id,
                        Username = user.Username,
                        ProfileImage = user.ProfileImage,
                        ProfileDescription = user.ProfileDescription,
                        Location = user.Location
                    };

                    using var httpClient = new HttpClient();
                    await httpClient.PostAsJsonAsync("http://localhost:5117/api/User/sync", syncPayload);
                    _logger.LogInformation("Successfully synced user data with User Service");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync user data with User Service. Continuing with auth update.");
                }

                // Generate new token if username was updated
                string? token = null;
                if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
                {
                    token = _jwt.GenerateToken(user);
                }


                return Ok(new 
                { 
                    Success = true,
                    Token = token,
                    User = new 
                    {
                        Id = user.Id,
                        Username = user.Username,
                        ProfileImage = user.ProfileImage,
                        ProfileDescription = user.ProfileDescription,
                        Location = user.Location
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user data in Auth Service");
                return StatusCode(500, "An error occurred while updating user data");
            }
        }

        public class UserUpdateRequest
        {
            public string? Username { get; set; }
            public string? ProfileImage { get; set; }
            public string? ProfileDescription { get; set; }
            public string? Location { get; set; }
        }
    }
}