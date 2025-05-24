using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using notification_service.Data;
using notification_service.Models;
using notification_service.Models.Dtos;
using notification_service.Services;

namespace notification_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<NotificationController> _logger;
        private readonly ISubscriptionService _subscriptionService;

        public NotificationController(
            AppDbContext context,
            IPushNotificationService pushNotificationService,
            ILogger<NotificationController> logger,
            ISubscriptionService subscriptionService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
            _subscriptionService = subscriptionService;
        }

        [HttpPost("push/subscribe")]
        [Authorize]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto subscriptionDto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                {
                    return Unauthorized("User ID not found in token");
                }

                await _subscriptionService.AddSubscriptionAsync(userId, subscriptionDto);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to push notifications");
                return StatusCode(500, "An error occurred while subscribing to push notifications");
            }
        }

        [HttpPost("push/unsubscribe")]
        [Authorize]
        public async Task<IActionResult> Unsubscribe([FromBody] string endpoint)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                {
                    return Unauthorized("User ID not found in token");
                }

                await _subscriptionService.RemoveSubscriptionAsync(userId, endpoint);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from push notifications");
                return StatusCode(500, "An error occurred while unsubscribing from push notifications");
            }
        }

        [HttpPost("send")]
        [Authorize]
        public async Task<IActionResult> SendNotification([FromBody] NotificationDto notificationDto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                {
                    return Unauthorized("User ID not found in token");
                }

                // Save notification to database
                var notification = new Notification
                {
                    UserId = userId.ToString(),
                    Title = notificationDto.Title,
                    Message = notificationDto.Body,
                    IsRead = false,
                    Timestamp = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Send push notification
                await _pushNotificationService.SendNotificationToUser(
                    userId,
                    notificationDto.Title,
                    notificationDto.Body,
                    notificationDto.Url);

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification");
                return StatusCode(500, "An error occurred while sending the notification");
            }
        }

        [HttpPost("send-to-user/{userId}")]
        [Authorize]
        public async Task<IActionResult> SendToUser(int userId, [FromBody] NotificationDto notification)
        {
            try
            {
                // Get all active subscriptions for the target user
                var subscriptions = await _context.PushSubscriptions
                    .Where(s => s.UserId == userId.ToString() && (!s.ExpiresAt.HasValue || s.ExpiresAt > DateTime.UtcNow))
                    .ToListAsync();

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        await _pushNotificationService.SendNotificationAsync(
                            new PushSubscriptionDto
                            {
                                Endpoint = subscription.Endpoint,
                                ExpirationTime = subscription.ExpiresAt,
                                Keys = new PushSubscriptionKeys
                                {
                                    P256Dh = subscription.P256Dh,
                                    Auth = subscription.Auth
                                }
                            },
                            notification);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending notification to subscription {subscription.Id}");
                        // Mark expired or invalid subscriptions
                        subscription.ExpiresAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user");
                return StatusCode(500, "Error sending notification to user");
            }
        }

        [HttpPost("test")]
        [Authorize]
        public async Task<IActionResult> SendTestNotification()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                if (userId == 0)
                {
                    return Unauthorized("User ID not found in token");
                }

                // Send test notification to the current user
                await _pushNotificationService.SendNotificationToUser(
                    userId,
                    "Test Notification",
                    "This is a test notification from our service!",
                    "/");

                return Ok("Test notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, "Error sending test notification");
            }
        }
    }
}