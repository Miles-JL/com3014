using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Auth;
using Shared.Models;
using ChatroomService.Data;

public class ChatHandler
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;

    public ChatHandler(RequestDelegate next, IServiceProvider serviceProvider)
    {
        _next = next;
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var token = context.Request.Query["token"].ToString();
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
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            username = user?.Username ?? principal.Identity?.Name ?? Guid.NewGuid().ToString();
        }

        var roomIdStr = context.Request.Query["roomId"].ToString();
        if (!int.TryParse(roomIdStr, out int roomId))
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleChatAsync(webSocket, username, roomId);
    }

    private static readonly Dictionary<int, List<WebSocket>> RoomSockets = new();

    private async Task HandleChatAsync(WebSocket webSocket, string username, int roomId)
    {
        if (!RoomSockets.ContainsKey(roomId))
            RoomSockets[roomId] = new List<WebSocket>();

        RoomSockets[roomId].Add(webSocket);

        var buffer = new byte[1024 * 4];
        var closeReceived = false;

        while (!closeReceived && webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                closeReceived = true;
                break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var chatMessage = new
            {
                Username = username,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(chatMessage);

            var tasks = RoomSockets[roomId]
                .Where(s => s.State == WebSocketState.Open)
                .Select(s => s.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                ));

            await Task.WhenAll(tasks);
        }

        RoomSockets[roomId].Remove(webSocket);
        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
    }
}