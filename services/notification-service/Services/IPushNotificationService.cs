// services/notification-service/Services/IPushNotificationService.cs
using System.Threading.Tasks;
using notification_service.Models.Dtos;

namespace notification_service.Services
{
    public interface IPushNotificationService
    {
        Task SendNotificationToUser(int userId, string title, string message, string? url = null);
        Task SendNotificationAsync(PushSubscriptionDto subscription, NotificationDto notification);
    }
}