using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using MessageService.Data;
using System.Security.Claims;

namespace MessageService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly AppDbContext _db;

    public MessageController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/message/send
    [HttpPost("send")]
    [Authorize]
    public async Task<IActionResult> SendMessage([FromBody] DirectMessage message)
    {
        var senderIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(senderIdClaim) || !int.TryParse(senderIdClaim, out int senderId))
            return Unauthorized();

        message.SenderId = senderId;
        message.Timestamp = DateTime.UtcNow;

        _db.DirectMessages.Add(message);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Message sent" });
    }

    // GET /api/message/history/{userId}
    [HttpGet("history/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetHistory(int userId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int currentId))
            return Unauthorized();

        var messages = await _db.DirectMessages
            .Where(m =>
                (m.SenderId == currentId && m.RecipientId == userId) ||
                (m.SenderId == userId && m.RecipientId == currentId))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return Ok(messages);
    }
}
