using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Auth;
using Shared.Models;
using ChatroomService.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

// DTO for receiving user details from user-service
public class UserInternalDto
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? ProfileImage { get; set; }
}

namespace ChatroomService.Handlers
{
    public class ChatHandler
    {
        private static readonly Dictionary<int, List<WebSocket>> RoomSockets = new Dictionary<int, List<WebSocket>>();
        private static readonly object RoomLock = new object();
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChatHandler> _logger;

        public ChatHandler(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, ILogger<ChatHandler> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
                _logger.LogWarning("WebSocket connection attempt without access token.");
                context.Response.StatusCode = 401;
                return;
            }

            var jwtService = context.RequestServices.GetRequiredService<JwtService>();
            var principal = jwtService.ValidateToken(token);
            if (principal == null)
            {
                _logger.LogWarning("WebSocket connection attempt with invalid token.");
                context.Response.StatusCode = 401;
                return;
            }

            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim in token.");
                context.Response.StatusCode = 401;
                return;
            }

            string username;
            string profileImage = string.Empty; // Initialize

            // Fetch user details from user-service and sync to local DB
            try
            {
                var client = _httpClientFactory.CreateClient();
                // TODO: Make user-service URL configurable
                var userServiceUrl = $"http://localhost:5117/api/user/internal/{userId}"; 
                _logger.LogInformation($"Fetching user details for ID {userId} from {userServiceUrl}");
                
                UserInternalDto userDetailsDto = null;
                try 
                {
                    userDetailsDto = await client.GetFromJsonAsync<UserInternalDto>(userServiceUrl);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, $"HTTP request to user-service failed for user ID {userId}. URL: {userServiceUrl}");
                    // Optionally, try to load from local DB as a fallback or deny connection
                }
                
                if (userDetailsDto != null)
                {
                    _logger.LogInformation($"Successfully fetched details from user-service for ID {userId}: Username='{userDetailsDto.Username}', ProfileImage='{userDetailsDto.ProfileImage}'");
                    username = userDetailsDto.Username ?? principal.Identity?.Name ?? $"User_{userId}";
                    profileImage = userDetailsDto.ProfileImage ?? string.Empty;

                    // Upsert into local chatroom_db.Users table
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var localUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        if (localUser != null)
                        {
                            _logger.LogInformation($"Updating existing user ID {userId} in chatroom_db.");
                            localUser.Username = username; // Sync username too
                            localUser.ProfileImage = profileImage;
                            localUser.LastUpdated = DateTime.UtcNow;
                        }
                        else
                        {
                            _logger.LogInformation($"Adding new user ID {userId} to chatroom_db.");
                            db.Users.Add(new User 
                            {
                                Id = userId, 
                                Username = username, 
                                ProfileImage = profileImage, 
                                CreatedAt = DateTime.UtcNow, 
                                LastUpdated = DateTime.UtcNow 
                                // Other fields can be default if not strictly needed by chatroom-service
                            });
                        }
                        await db.SaveChangesAsync();
                        _logger.LogInformation($"User ID {userId} synced to chatroom_db.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Failed to fetch details from user-service for ID {userId}. Attempting to load from local DB or use defaults.");
                    // Fallback: Try to load from local DB if user-service call failed
                    // This part is similar to the original logic but now it's a fallback.
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                        if (user != null)
                        {
                            username = user.Username ?? principal.Identity?.Name ?? Guid.NewGuid().ToString();
                            profileImage = user.ProfileImage ?? string.Empty;
                            _logger.LogInformation($"Loaded user {username} (ID: {userId}) from local chatroom_db. ProfileImage: '{profileImage}'");
                        }
                        else
                        {
                            // If still not found, or user-service failed and no local record, assign defaults
                            username = principal.Identity?.Name ?? Guid.NewGuid().ToString();
                            profileImage = string.Empty; // Default if no data found
                            _logger.LogWarning($"User ID {userId} not found in local DB either. Using defaults. Username: {username}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during user sync for ID {userId}");
                // Fallback to principal name if sync fails catastrophically
                username = principal.Identity?.Name ?? Guid.NewGuid().ToString();
                profileImage = string.Empty; // Default if sync fails
            }

            _logger.LogInformation($"User {username} (ID: {userId}) attempting to connect to chat. Final ProfileImage to be used: '{profileImage}'");

            var roomIdStr = context.Request.Query["roomId"].ToString();
            if (!int.TryParse(roomIdStr, out int roomId))
            {
                _logger.LogWarning("Invalid roomId query parameter.");
                context.Response.StatusCode = 400;
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation($"WebSocket connection established for user {username} in room {roomId}.");
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

                try
                {
                    // Create the chat message with the user's profile image
                    var chatMessage = new
                    {
                        sender = username,
                        profileImage = string.IsNullOrEmpty(profileImage) ? null : profileImage,
                        text = message,
                        timestamp = DateTime.UtcNow
                    };

                    var logger = _serviceProvider.GetRequiredService<ILogger<ChatHandler>>();
                    logger.LogInformation($"Sending message from {username} with profileImage: {profileImage}");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };
                    
                    var jsonMessage = JsonSerializer.Serialize(chatMessage, options);
                    logger.LogInformation($"Full message JSON: {jsonMessage}");

                    await BroadcastToRoom(roomId, jsonMessage);
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetRequiredService<ILogger<ChatHandler>>();
                    logger.LogError(ex, "Error creating or sending chat message");
                    throw;
                }

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
}