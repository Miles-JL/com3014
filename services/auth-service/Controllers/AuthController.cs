using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using Shared.Auth;
using AuthService.Data;
using System.Net.Http.Json;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
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
            Console.WriteLine($"[Login Sync Failed] {ex.Message}");
            // continue anyway
        }

        return Ok(new { token });
    }
}
