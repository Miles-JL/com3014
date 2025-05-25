using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Shared.Auth
{
    public class UserSyncService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserSyncService> _logger;
        private readonly HttpClient _httpClient;

        public UserSyncService(
            IConfiguration configuration,
            ILogger<UserSyncService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task SyncUserToServices(int userId, string username, string? profileImage = null, string? profileDescription = null, string? location = null)
        {
            try
            {
                var chatroomService = _configuration["Services:ChatroomService"] ?? "http://chatroom-service:5262";
                
                var syncRequest = new UserSyncRequest
                {
                    Id = userId,
                    Username = username,
                    ProfileImage = profileImage,
                    ProfileDescription = profileDescription,
                    Location = location
                };

                // Sync to chatroom service
                var response = await _httpClient.PostAsJsonAsync($"{chatroomService}/api/User/sync", syncRequest);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to sync user to chatroom-service. Status: {Status}", response.StatusCode);
                }
                else 
                {
                    _logger.LogInformation("User {UserId} synced to chatroom-service", userId);
                }
                
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user {UserId} to other services", userId);
            }
        }
    }
}