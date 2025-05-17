using JwtAuthApi.Data;
using JwtAuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JwtAuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatRoomController : ControllerBase
{
    private readonly AppDbContext _db;

    public ChatRoomController(AppDbContext db)
    {
        _db = db;
    }

    // Get all chat rooms
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatRoomResponse>>> GetChatRooms()
    {
        var rooms = await _db.ChatRooms
            .Where(r => r.IsActive)
            .Select(r => new ChatRoomResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                CreatorName = r.Creator.Username,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return rooms;
    }

    // Get specific chat room
    [HttpGet("{id}")]
    public async Task<ActionResult<ChatRoomResponse>> GetChatRoom(int id)
    {
        var room = await _db.ChatRooms
            .Where(r => r.Id == id && r.IsActive)
            .Select(r => new ChatRoomResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                CreatorName = r.Creator.Username,
                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (room == null)
            return NotFound();

        return room;
    }

    // Create new chat room
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ChatRoomResponse>> CreateChatRoom(CreateChatRoomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Chat room name is required");

        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return Unauthorized();

        var chatRoom = new ChatRoom
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CreatorId = user.Id,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.ChatRooms.Add(chatRoom);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetChatRoom),
            new { id = chatRoom.Id },
            new ChatRoomResponse
            {
                Id = chatRoom.Id,
                Name = chatRoom.Name,
                Description = chatRoom.Description,
                CreatorName = user.Username,
                CreatedAt = chatRoom.CreatedAt
            });
    }

    // Delete chat room (mark as inactive)
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteChatRoom(int id)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return Unauthorized();

        var chatRoom = await _db.ChatRooms.FindAsync(id);
        if (chatRoom == null)
            return NotFound();

        // Only the creator can delete a chat room
        if (chatRoom.CreatorId != user.Id)
            return Forbid();

        chatRoom.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

// DTOs
public class CreateChatRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ChatRoomResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}