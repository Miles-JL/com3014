using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Auth;
using Shared.Models;
using ChatroomService.Data;

public class ChatHandler
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly Dictionary<int, List<WebSocket>> RoomSockets = new();
    private static readonly object RoomLock = new();

    public ChatHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var token = context.Request.Query["access_token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 401;
            return;
        }

        var jwtService = context.RequestServices.GetRequiredService<JwtService>();
        var principal = jwtService.ValidateToken(token);
        if (principal == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        // Get user ID from token
        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        // Fetch latest username from DB
        string username;
        string profileImage = string.Empty;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            username = user?.Username ?? principal.Identity?.Name ?? Guid.NewGuid().ToString();
            profileImage = user?.ProfileImage ?? string.Empty;
        }

        var roomIdStr = context.Request.Query["roomId"].ToString();
        if (!int.TryParse(roomIdStr, out int roomId))
        {
            context.Response.StatusCode = 400;
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleChatAsync(webSocket, username, profileImage, roomId);
    }

    private async Task HandleChatAsync(WebSocket webSocket, string username, string profileImage, int roomId)
    {
        lock (RoomLock)
        {
            if (!RoomSockets.ContainsKey(roomId))
                RoomSockets[roomId] = new List<WebSocket>();
            RoomSockets[roomId].Add(webSocket);
        }

        // Notify join
        var joinMessage = new
        {
            type = "system",
            text = $"{username} joined the room",
            timestamp = DateTime.UtcNow
        };
        await BroadcastToRoom(roomId, JsonSerializer.Serialize(joinMessage));

        var buffer = new byte[1024 * 4];
        var closeReceived = false;
        var heartbeatInterval = TimeSpan.FromSeconds(30);
        var receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var heartbeatTimer = Task.Delay(heartbeatInterval);

        while (!closeReceived && webSocket.State == WebSocketState.Open)
        {
            var completed = await Task.WhenAny(receiveTask, heartbeatTimer);

            // Timeout reached → close due to inactivity
            if (completed == heartbeatTimer)
            {
                // Optionally: send a system message about timeout
                break;
            }

            var result = await receiveTask;
            if (result.MessageType == WebSocketMessageType.Close)
            {
                closeReceived = true;
                break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // Reset heartbeat timer on any message
            heartbeatTimer = Task.Delay(heartbeatInterval);

            // Ignore ping messages
            if (message == "__ping__")
            {
                receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                continue;
            }

            var chatMessage = new
            {
                sender = username,
                profileImage = profileImage,
                text = message,
                timestamp = DateTime.UtcNow
            };
            await BroadcastToRoom(roomId, JsonSerializer.Serialize(chatMessage));

            receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        // Remove socket from room
        lock (RoomLock)
        {
            if (RoomSockets.ContainsKey(roomId))
                RoomSockets[roomId].Remove(webSocket);
        }

        // Notify leave
        var leaveMessage = new
        {
            type = "system",
            text = $"{username} left the room",
            timestamp = DateTime.UtcNow
        };
        await BroadcastToRoom(roomId, JsonSerializer.Serialize(leaveMessage));

        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
    }

    private static async Task BroadcastToRoom(int roomId, string json)
    {
        List<WebSocket> sockets;
        lock (RoomLock)
        {
            if (!RoomSockets.ContainsKey(roomId)) return;
            // Remove closed sockets
            RoomSockets[roomId].RemoveAll(s => s.State != WebSocketState.Open);
            sockets = RoomSockets[roomId].ToList();
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        var tasks = sockets.Select(s =>
            s.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
        );
        await Task.WhenAll(tasks);
    }
}