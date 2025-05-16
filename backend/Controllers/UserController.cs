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

        if (!string.IsNullOrEmpty(request.ProfileDescription))
            user.ProfileDescription = request.ProfileDescription;
            
        if (!string.IsNullOrEmpty(request.Location))
            user.Location = request.Location;
            
        user.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        
        return Ok(user);
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