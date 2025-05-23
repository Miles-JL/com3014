using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Shared.Auth;
using Shared.Models;

namespace RealtimeService.Handlers;

/// <summary>
/// Handles real-time WebSocket connections for direct messaging between users.
/// Validates JWT tokens, maintains user connections, forwards messages, and syncs with message-service.
/// </summary>
public class DmHandler
{
    private readonly Dictionary<int, WebSocket> _connections = new(); // Active user WebSocket connections
    private readonly IServiceProvider _services;

    public DmHandler(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Handles a full lifecycle of a WebSocket session:
    /// - Authenticates the user
    /// - Receives messages and pings
    /// - Broadcasts messages to recipients
    /// - Sends persisted messages to message-service
    /// </summary>
    public async Task HandleWebSocketAsync(HttpContext context)
    {
        Console.WriteLine("WebSocket request received");

        // Extract token from query string
        var token = context.Request.Query["access_token"];
        Console.WriteLine("Token received: " + token);

        var jwt = context.RequestServices.GetRequiredService<JwtService>();
        var principal = jwt.ValidateToken(token!);

        if (principal == null)
        {
            Console.WriteLine("Token validation failed");
            context.Response.StatusCode = 401;
            return;
        }

        // Attempt to extract user ID from claims
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                           ?? principal.FindFirst("nameidentifier")
                           ?? principal.FindFirst("uid");

        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            Console.WriteLine("Invalid or missing user ID claim");
            context.Response.StatusCode = 401;
            return;
        }

        Console.WriteLine($"Authenticated user ID: {userId}");

        // Accept WebSocket connection and register user
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        _connections[userId] = socket;
        Console.WriteLine($"WebSocket connection established for user {userId}");

        // Setup message receive loop and heartbeat timeout
        var buffer = new byte[1024 * 4];
        var receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var heartbeatInterval = TimeSpan.FromSeconds(30);
        var heartbeatTimer = Task.Delay(heartbeatInterval);

        while (socket.State == WebSocketState.Open)
        {
            var completed = await Task.WhenAny(receiveTask, heartbeatTimer);

            // Timeout reached → close due to inactivity
            if (completed == heartbeatTimer)
            {
                Console.WriteLine($"[Heartbeat] No activity from user {userId} in {heartbeatInterval.TotalSeconds} seconds. Closing.");
                break;
            }

            var result = await receiveTask;
            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"[DMHandler] Raw message from user {userId}: {text}");

            var msg = JsonSerializer.Deserialize<RealtimeMessage>(text);
            if (msg == null)
            {
                Console.WriteLine("[DMHandler] Failed to deserialize RealtimeMessage.");
                receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                continue;
            }

            // Reset heartbeat timer on valid message
            heartbeatTimer = Task.Delay(heartbeatInterval);

            // Handle keep-alive ping
            if (msg.Text == "__ping__")
            {
                Console.WriteLine($"[Ping] Received heartbeat from user {userId}");
                receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                continue;
            }

            Console.WriteLine($"[DMHandler] Parsed message: from {msg.SenderId} to {msg.RecipientId}, content = '{msg.Text}'");

            // Forward message to recipient if they're connected
            if (_connections.TryGetValue(msg.RecipientId, out var recipientSocket) &&
                recipientSocket.State == WebSocketState.Open)
            {
                var payload = JsonSerializer.Serialize(msg);
                var bytes = Encoding.UTF8.GetBytes(payload);
                await recipientSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                // Optional: trigger offline notification logic here
                Console.WriteLine($"User {msg.RecipientId} is not connected");
            }

            // Send message to message-service for persistence
            await SendToMessageServiceAsync(msg);

            // Prepare for next receive
            receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        // Clean up after disconnect
        _connections.Remove(userId);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
        Console.WriteLine($"WebSocket connection closed for user {userId}");
    }

    /// <summary>
    /// Sends a message to the message-service via HTTP POST.
    /// Includes internal API key for service-to-service authorization.
    /// </summary>
    private async Task SendToMessageServiceAsync(RealtimeMessage msg)
    {
        if (msg.Text == "__ping__") return;

        try
        {
            using var scope = _services.CreateScope();
            var client = new HttpClient();

            // Use internal API key to bypass JWT requirement
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", "realtime-secret-123");

            var directMessage = new DirectMessage
            {
                SenderId = msg.SenderId,
                Sender = msg.Sender,
                RecipientId = msg.RecipientId,
                Content = msg.Text,
                Timestamp = msg.Timestamp,
                IsRead = false
            };

            var response = await client.PostAsJsonAsync("http://localhost:5199/api/message/send", directMessage);
            Console.WriteLine($"Sent message to message-service (Status: {response.StatusCode})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to message-service: {ex.Message}");
        }
    }
}

/// <summary>
/// Serializable message format sent over WebSocket.
/// </summary>
public class RealtimeMessage
{
    [JsonPropertyName("senderId")]
    public int SenderId { get; set; }

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = "";
    public int profileImage { get; set; }

    [JsonPropertyName("recipientId")]
    public int RecipientId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
