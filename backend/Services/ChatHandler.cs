using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using JwtAuthApi.Data;
using JwtAuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace JwtAuthApi.Services;

public class ChatHandler
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly IServiceProvider _serviceProvider;

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

        var token = context.Request.Query["token"];
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

        var username = principal.Identity?.Name ?? Guid.NewGuid().ToString();
        var socket = await context.WebSockets.AcceptWebSocketAsync();

        _sockets.TryAdd(username, socket);

        await ReceiveMessagesAsync(username, socket);

        _sockets.TryRemove(username, out _);
    }

    private async Task ReceiveMessagesAsync(string username, WebSocket socket)
    {
        var buffer = new byte[1024 * 4];
        
        // Fetch user profile info
        User? userProfile;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userProfile = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
            
            // Create a message object with user details
            var messageObject = new
            {
                text = messageText,
                sender = username,
                profileImage = userProfile?.ProfileImage ?? "",
                timestamp = DateTime.UtcNow
            };
            
            await BroadcastMessageAsync(JsonSerializer.Serialize(messageObject));
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
    }

    private async Task BroadcastMessageAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var tasks = _sockets.Values.Select(socket =>
            socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None)
        );
        await Task.WhenAll(tasks);
    }
}