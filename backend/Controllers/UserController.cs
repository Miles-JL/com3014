using JwtAuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using JwtAuthApi.Data;

namespace JwtAuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    // Get current user profile
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<User>> GetProfile()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return NotFound();

        // Don't send password hash
        user.PasswordHash = string.Empty;
        
        return user;
    }

    // Update profile
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return NotFound();

        // Update username if provided and different
        if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
        {
            // Check if username is already taken
            if (await _db.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { message = "Username is already taken" });
                
            user.Username = request.Username;
        }
        
        // Update profile description if provided
        if (request.ProfileDescription != null) // Allow empty string
            user.ProfileDescription = request.ProfileDescription;
            
        user.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        
        return Ok(new { 
            username = user.Username,
            profileDescription = user.ProfileDescription,
            profileImage = user.ProfileImage
        });
    }

    // Upload profile image
    [HttpPost("profile-image")]
    [Authorize]
    public async Task<IActionResult> UploadProfileImage([FromForm] IFormFile file)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        user.ProfileImage = $"/uploads/{fileName}";
        user.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { profileImage = user.ProfileImage });
    }
}