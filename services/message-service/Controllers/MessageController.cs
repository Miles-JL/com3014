using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using MessageService.Data;
using System.Security.Claims;

namespace MessageService.Controllers;

/// <summary>
/// Controller for handling direct messages between users.
/// This controller provides endpoints for sending and retrieving messages.
/// It uses JWT authentication for user verification and an internal API key for internal service communication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    /// <summary>
    /// Initialises the MessageController with the provided database context and configuration.
    /// </summary>
    /// <param name="db">The database context for accessing message data.</param>
    /// <param name="config">The configuration for accessing application settings.</param>
    public MessageController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Sends a direct message from one user to another.
    /// Called by the realtime service using an internal API key or by an authenticated user.
    /// </summary>
    /// <param name="message">The DirectMessage to save.</param>
    /// <returns>200 OK if saved, or 401 Unauthorized if validation fails.</returns>
    [HttpPost("send")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> SendMessage([FromBody] DirectMessage message)
    {
        var internalKeyFromHeader = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        var expectedKey = _config["InternalApiKey"];

        // Internal API key check
        if (internalKeyFromHeader == expectedKey)
        {
            Console.WriteLine("[RECEIVED] Message via internal API key (realtime-service)");
        }
        else
        {
            // JWT authentication check
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int parsedUserId) || parsedUserId != message.SenderId)
            {
                return Unauthorized("Invalid user JWT or mismatched sender ID");
            }

            if (parsedUserId != message.SenderId)
                return Unauthorized("Sender ID does not match authenticated user");

            Console.WriteLine("[RECEIVED] Message from authenticated user");
        }

        message.Timestamp = DateTime.UtcNow;
        _db.DirectMessages.Add(message);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Message saved" });
    }



    /// <summary>
    /// Returns the message history between the current authenticated user and another recipient.
    /// Supports pagination using a `before` timestamp.
    /// </summary>
    /// <param name="recipientId">ID of the other user in the conversation.</param>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="before">Optional upper time bound for pagination.</param>
    /// <returns>Chronologically ordered list of DirectMessages.</returns>
    [HttpGet("history")]
    [Authorize] // Require JWT for this endpoint
    public async Task<IActionResult> GetHistory([FromQuery] int recipientId, [FromQuery] int limit = 50, [FromQuery] DateTime? before = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int currentId))
            return Unauthorized();

        var query = _db.DirectMessages
            .Where(m =>
                (m.SenderId == currentId && m.RecipientId == recipientId) ||
                (m.SenderId == recipientId && m.RecipientId == currentId));

        if (before.HasValue)
            query = query.Where(m => m.Timestamp < before.Value);

        var messages = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .OrderBy(m => m.Timestamp) // Final ordering to get the correct order
            .ToListAsync();

        return Ok(messages);
    }
}
