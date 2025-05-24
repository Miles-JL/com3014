using System.Net.WebSockets;
using notification_service.Models;

namespace notification_service.Services;

public interface IWebSocketManager
{
    Task AddConnection(int userId, WebSocket webSocket);
    Task RemoveConnection(int userId, WebSocket webSocket);
    Task SendNotificationToUser(int userId, Notification notification);
}
