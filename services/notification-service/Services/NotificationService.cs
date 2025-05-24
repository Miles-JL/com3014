using Microsoft.EntityFrameworkCore;
using notification_service.Data;
using notification_service.Models;
using notification_service.Models.Dtos;

namespace notification_service.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly IWebSocketManager _webSocketManager;

    public NotificationService(
        AppDbContext context, 
        ILogger<NotificationService> logger,
        IWebSocketManager webSocketManager)
    {
        _context = context;
        _logger = logger;
        _webSocketManager = webSocketManager;
    }

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto)
    {
        var notification = new Notification
        {
            UserId = dto.RecipientId.ToString(),
            Title = dto.Type,
            Message = dto.Message,
            IsRead = false,
            Timestamp = DateTime.UtcNow,
            Metadata = dto.Metadata
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created notification {NotificationId} for user {UserId}", notification.Id, dto.RecipientId);

        // Try to send real-time notification if user is online
        await _webSocketManager.SendNotificationToUser(dto.RecipientId, notification);

        return MapToDto(notification);
    }

    public async Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(int userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId.ToString() && !n.IsRead)
            .OrderByDescending(n => n.Timestamp)
            .ToListAsync();

        return notifications.Select(MapToDto);
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId.ToString());

        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked notification {NotificationId} as read for user {UserId}", notificationId, userId);
        }
    }
    
    public async Task MarkAllAsReadAsync(int userId)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId.ToString() && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        if (unreadNotifications.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked all notifications as read for user {UserId}", userId);
        }
    }

    private static NotificationDto MapToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Title = notification.Title,
        Body = notification.Message,
        Url = notification.Url,
        IsRead = notification.IsRead,
        Timestamp = notification.Timestamp
    };
}
