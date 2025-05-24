using notification_service.Models;
using notification_service.Models.Dtos;

namespace notification_service.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto);
    Task<IEnumerable<NotificationDto>> GetUnreadNotificationsAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}
