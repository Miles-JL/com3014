using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JwtAuthApi.Data;
using JwtAuthApi.Models;
using JwtAuthApi.Services;
using System.Security.Cryptography;
using System.Text;

namespace JwtAuthApi.Controllers;

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

        user.PasswordHash = ComputeSha256Hash(user.PasswordHash);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok("User registered");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] User credentials)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == credentials.Username);
        if (user == null || user.PasswordHash != ComputeSha256Hash(credentials.PasswordHash))
            return Unauthorized();

        var token = _jwt.GenerateToken(user);
        return Ok(new { token });
    }

    private string ComputeSha256Hash(string rawData)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToBase64String(bytes);
    }
}