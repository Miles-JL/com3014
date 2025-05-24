using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using notification_service.Models;
using notification_service.Models.Dtos;

namespace notification_service.Services;

public class WebSocketManager : IWebSocketManager
{
    private readonly ILogger<WebSocketManager> _logger;
    private static readonly Dictionary<int, List<WebSocket>> _userConnections = new();
    private static readonly object _lock = new();

    public WebSocketManager(ILogger<WebSocketManager> logger)
    {
        _logger = logger;
    }

    public async Task AddConnection(int userId, WebSocket webSocket)
    {
        lock (_lock)
        {
            if (!_userConnections.ContainsKey(userId))
            {
                _userConnections[userId] = new List<WebSocket>();
            }

            // Clean up any dead connections
            _userConnections[userId] = _userConnections[userId]
                .Where(ws => ws.State == WebSocketState.Open)
                .ToList();

            // Don't add duplicate connections
            if (webSocket != null && webSocket.State == WebSocketState.Open && 
                !_userConnections[userId].Any(ws => ws == webSocket || ws.State == WebSocketState.Open))
            {
                _userConnections[userId].Add(webSocket);
                _logger.LogInformation("WebSocket connection added for user {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("WebSocket connection not added - already exists or invalid state");
                return;
            }
        }

        // Keep the connection alive
        await HandleConnectionLifecycle(userId, webSocket);
    }

    private async Task HandleConnectionLifecycle(int userId, WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                        "Closed by client", CancellationToken.None);
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation("Received message from user {UserId}: {Message}", userId, message);
                    
                    try
                    {
                        // Parse the incoming message
                        using var doc = JsonDocument.Parse(message);
                        var root = doc.RootElement;
                        
                        // Handle different message types
                        if (root.TryGetProperty("type", out var typeProperty))
                        {
                            var messageType = typeProperty.GetString()?.ToLowerInvariant();
                            
                            switch (messageType)
                            {
                                case "ping":
                                    var pongResponse = new { type = "pong", timestamp = DateTime.UtcNow };
                                    await SendMessage(webSocket, pongResponse);
                                    break;
                                    
                                case "test":
                                    var testResponse = new 
                                    { 
                                        type = "test_response", 
                                        message = "Hello from server!",
                                        receivedAt = DateTime.UtcNow,
                                        yourMessage = message
                                    };
                                    await SendMessage(webSocket, testResponse);
                                    break;
                                    
                                default:
                                    _logger.LogInformation("Unknown message type: {MessageType}", messageType);
                                    var unknownResponse = new { type = "error", message = $"Unknown message type: {messageType}" };
                                    await SendMessage(webSocket, unknownResponse);
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing WebSocket message");
                        var errorResponse = new { type = "error", message = "Error processing message", details = ex.Message };
                        await SendMessage(webSocket, errorResponse);
                    }
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            // Client disconnected unexpectedly
            _logger.LogInformation("Client {UserId} disconnected unexpectedly", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket connection for user {UserId}", userId);
        }
        finally
        {
            await RemoveConnection(userId, webSocket);
        }
    }

    public async Task RemoveConnection(int userId, WebSocket webSocket)
    {
        lock (_lock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.RemoveAll(ws => ws == webSocket);
                _logger.LogInformation("WebSocket connection removed for user {UserId}", userId);

                if (connections != null && !connections.Any())
                {
                    _userConnections.Remove(userId);
                }
            }
        }

        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.CloseAsync(
                closeStatus: WebSocketCloseStatus.NormalClosure,
                statusDescription: "Closing connection",
                cancellationToken: CancellationToken.None);
        }
    }

    private async Task SendMessage(WebSocket webSocket, object message)
    {
        if (webSocket?.State != WebSocketState.Open)
            return;
            
        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebSocket message");
        }
    }

    public async Task SendNotificationToUser(int userId, Notification notification)
    {
        if (notification == null)
        {
            _logger.LogWarning("Attempted to send null notification to user {UserId}", userId);
            return;
        }
        
        List<WebSocket>? connections;
        lock (_lock)
        {
            if (!_userConnections.TryGetValue(userId, out connections) || connections == null)
            {
                _logger.LogDebug("No active WebSocket connections for user {UserId}", userId);
                return;
            }
            
            // Filter out closed connections
            connections = connections.Where(ws => ws != null && ws.State == WebSocketState.Open).ToList();
            _userConnections[userId] = connections;
            
            if (!connections.Any())
            {
                _userConnections.Remove(userId);
                return;
            }
        }

        string message;
        try
        {
            var notificationDto = new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Body = notification.Message,
                Url = notification.Url,
                IsRead = notification.IsRead,
                Timestamp = notification.Timestamp
            };

            message = JsonSerializer.Serialize(new
            {
                type = "notification",
                data = notificationDto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize notification for user {UserId}", userId);
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);
        var failedConnections = new List<WebSocket>();
        
        foreach (var connection in connections.ToList()) // Create a copy to avoid modification during iteration
        {
            try
            {
                if (connection?.State == WebSocketState.Open)
                {
                    await connection.SendAsync(
                        segment,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                else if (connection != null)
                {
                    failedConnections.Add(connection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WebSocket message to user {UserId}", userId);
                if (connection != null)
                {
                    failedConnections.Add(connection);
                }
            }
        }
        
        // Clean up failed connections
        if (failedConnections.Any())
        {
            lock (_lock)
            {
                if (_userConnections.TryGetValue(userId, out var currentConnections))
                {
                    foreach (var failedConnection in failedConnections)
                    {
                        currentConnections.Remove(failedConnection);
                    }
                    
                    if (!currentConnections.Any())
                    {
                        _userConnections.Remove(userId);
                    }
                }
            }
        }
    }

    private class WebSocketMessage
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
