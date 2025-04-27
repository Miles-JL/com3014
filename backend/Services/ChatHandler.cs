using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

namespace JwtAuthApi.Services;

public class ChatHandler
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

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

        var userId = principal.Identity?.Name ?? Guid.NewGuid().ToString();
        var socket = await context.WebSockets.AcceptWebSocketAsync();

        _sockets.TryAdd(userId, socket);

        await ReceiveMessagesAsync(userId, socket);

        _sockets.TryRemove(userId, out _);
    }

    private async Task ReceiveMessagesAsync(string userId, WebSocket socket)
    {
        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await BroadcastMessageAsync($"{userId}: {message}");
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
