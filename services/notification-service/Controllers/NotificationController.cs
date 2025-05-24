using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using notification_service.Models.Dtos;
using notification_service.Services;

namespace notification_service.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;
    private readonly IWebSocketManager _webSocketManager;

    public NotificationController(
        INotificationService notificationService,
        ILogger<NotificationController> logger,
        IWebSocketManager webSocketManager)
    {
        _notificationService = notificationService;
        _logger = logger;
        _webSocketManager = webSocketManager;
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationService.GetUnreadNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDto dto)
    {
        // This endpoint is called internally by other services
        if (!IsInternalRequest() && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var notification = await _notificationService.CreateNotificationAsync(dto);
        return CreatedAtAction(nameof(GetUnreadNotifications), new { id = notification.Id }, notification);
    }

    [HttpPost("mark-read/{notificationId}")]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAsReadAsync(notificationId, userId);
        return NoContent();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        // Get user ID from claims
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "uid") ??
                         User.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ??
                         User.Claims.FirstOrDefault(c => c.Type == "sub") ??
                         User.Claims.FirstOrDefault(c => c.Type == "userId");
        
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException($"Invalid user ID in token. Available claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}"))}");
        }

        return userId;
    }

    private bool IsInternalRequest()
    {
        // Check for internal API key header
        return Request.Headers.TryGetValue("X-Internal-Api-Key", out var apiKey) &&
               string.Equals(apiKey, Environment.GetEnvironmentVariable("INTERNAL_API_KEY"));
    }
}
