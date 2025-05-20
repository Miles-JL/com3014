using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Shared.Models;
using ChatroomService.Data;


namespace ChatRoomService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatRoomController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChatRoomController> _logger;

    public ChatRoomController(AppDbContext db, ILogger<ChatRoomController> logger)
    {
        _db = db;
        _logger = logger;
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

        // Get the user ID from the token (NameIdentifier claim)
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID claim: {UserIdClaim}", userIdClaim);
            return Unauthorized("Invalid user ID claim");
        }

        // Get username from Name claim for creator name
        var username = User.Identity?.Name ?? "Unknown";
        _logger.LogInformation("Creating room for user ID: {UserId}, Username: {Username}", userId, username);

        // Look up user by ID instead of username
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogInformation("User {UserId} not found in local DB, creating placeholder", userId);
            // User doesn't exist in chatroom DB, create a placeholder user
            user = new User
            {
                Id = userId,
                Username = username
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var chatRoom = new ChatRoom
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CreatorId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.ChatRooms.Add(chatRoom);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created chat room: {RoomId} by user {UserId}", chatRoom.Id, userId);

        return CreatedAtAction(
            nameof(GetChatRoom),
            new { id = chatRoom.Id },
            new ChatRoomResponse
            {
                Id = chatRoom.Id,
                Name = chatRoom.Name,
                Description = chatRoom.Description,
                CreatorName = username,
                CreatedAt = chatRoom.CreatedAt
            });
    }

    // Delete chat room (mark as inactive)
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteChatRoom(int id)
    {
        // Get the user ID from the token (NameIdentifier claim)
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid user ID claim");

        var chatRoom = await _db.ChatRooms.FindAsync(id);
        if (chatRoom == null)
            return NotFound();

        // Only the creator can delete a chat room
        if (chatRoom.CreatorId != userId)
            return Forbid();

        chatRoom.IsActive = false;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted chat room: {RoomId} by user {UserId}", id, userId);

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