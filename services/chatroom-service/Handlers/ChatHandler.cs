using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatroomService.Data;
using Shared.Models;
using Shared.Auth;

namespace ChatroomService.Handlers;

public class ChatHandler
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, WebSocket>> _rooms = new();
    // Store room messages for history
    private readonly ConcurrentDictionary<int, List<object>> _roomMessages = new();
    private readonly IServiceProvider _serviceProvider;
    
    // Track when a user last joined a room to prevent duplicate notifications
    private readonly ConcurrentDictionary<string, Dictionary<int, DateTime>> _lastJoinTime = new();

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

        var roomIdStr = context.Request.Query["roomId"];
        if (string.IsNullOrEmpty(roomIdStr) || !int.TryParse(roomIdStr, out int roomId))
        {
            context.Response.StatusCode = 400;
            return;
        }

        var jwtService = context.RequestServices.GetRequiredService<JwtService>();
        var principal = jwtService.ValidateToken(token!);
        if (principal == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        // Ensure the room exists
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roomExists = await db.ChatRooms.AnyAsync(r => r.Id == roomId && r.IsActive);
            if (!roomExists)
            {
                context.Response.StatusCode = 404; // Room not found
                return;
            }
        }

        var username = principal.Identity?.Name ?? Guid.NewGuid().ToString();
        var socket = await context.WebSockets.AcceptWebSocketAsync();

        // Add socket to room
        var room = _rooms.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, WebSocket>());
        
        // Check if this is a reconnection or if too soon since last join
        bool isReconnect = false;
        string userKey = $"{username}_{roomId}";
        
        if (!_lastJoinTime.TryGetValue(username, out var roomJoinTimes))
        {
            roomJoinTimes = new Dictionary<int, DateTime>();
            _lastJoinTime[username] = roomJoinTimes;
        }
        
        if (roomJoinTimes.TryGetValue(roomId, out DateTime lastJoin))
        {
            // If joined in the last 5 seconds, consider it a reconnection
            if ((DateTime.UtcNow - lastJoin).TotalSeconds < 5)
            {
                isReconnect = true;
            }
        }
        
        // Update the last join time
        roomJoinTimes[roomId] = DateTime.UtcNow;
        
        // Add socket to room
        room.TryAdd(username, socket);

        // Send message history to the newly connected user
        if (_roomMessages.TryGetValue(roomId, out var messages))
        {
            var historyMessage = new
            {
                type = "history",
                messages = messages
            };
            
            await SendToClientAsync(socket, JsonSerializer.Serialize(historyMessage));
        }
        else
        {
            // Initialize message list for this room
            _roomMessages[roomId] = new List<object>();
        }

        // Notify all users in the room that a new user joined (unless reconnecting)
        if (!isReconnect)
        {
            var joinMessage = new
            {
                type = "system",
                text = $"{username} joined the room",
                timestamp = DateTime.UtcNow
            };
            
            // Add join message to room history
            _roomMessages[roomId].Add(joinMessage);
            
            await BroadcastToRoomAsync(roomId, JsonSerializer.Serialize(joinMessage));
        }

        await ProcessMessagesAsync(roomId, username, socket);

        // Remove the socket when done
        if (_rooms.TryGetValue(roomId, out var roomSockets))
        {
            roomSockets.TryRemove(username, out _);
            
            // If room is empty, remove it
            if (roomSockets.IsEmpty)
            {
                _rooms.TryRemove(roomId, out _);
                // Optionally, you can choose to clear message history when room is empty
                // _roomMessages.TryRemove(roomId, out _);
            }
            else
            {
                // Only send leave message if there are still people in the room
                var leaveMessage = new
                {
                    type = "system",
                    text = $"{username} left the room",
                    timestamp = DateTime.UtcNow
                };
                
                // Add leave message to room history
                _roomMessages[roomId].Add(leaveMessage);
                
                await BroadcastToRoomAsync(roomId, JsonSerializer.Serialize(leaveMessage));
            }
        }
        
        // Clean up user join tracking
        if (_lastJoinTime.TryGetValue(username, out var userRooms))
        {
            userRooms.Remove(roomId);
            if (userRooms.Count == 0)
            {
                _lastJoinTime.TryRemove(username, out _);
            }
        }
    }

    private async Task ProcessMessagesAsync(int roomId, string username, WebSocket socket)
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
            
            // Add message to room history
            if (_roomMessages.TryGetValue(roomId, out var messages))
            {
                messages.Add(messageObject);
                
                // Limit history to last 100 messages
                if (messages.Count > 100)
                {
                    messages.RemoveAt(0);
                }
            }
            
            await BroadcastToRoomAsync(roomId, JsonSerializer.Serialize(messageObject));
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
    }

    private async Task SendToClientAsync(WebSocket socket, string message)
    {
        if (socket.State == WebSocketState.Open)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }

    private async Task BroadcastToRoomAsync(int roomId, string message)
    {
        if (!_rooms.TryGetValue(roomId, out var roomSockets))
            return;
            
        var buffer = Encoding.UTF8.GetBytes(message);
        var tasks = roomSockets.Values
            .Where(socket => socket.State == WebSocketState.Open)
            .Select(socket => 
                socket.SendAsync(
                    new ArraySegment<byte>(buffer), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None
                )
            );
        
        await Task.WhenAll(tasks);
    }
}