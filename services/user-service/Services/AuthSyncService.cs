using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace UserService.Services
{
    public class AuthSyncService : IAuthSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly string _authServiceBaseUrl;

        public AuthSyncService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("AuthServiceClient");
            _authServiceBaseUrl = configuration["ServiceUrls:AuthService"] ?? "http://auth-service:5106";
        }

        public async Task<bool> UpdateUserProfileAsync(UserProfileUpdateDto userProfileUpdate, string accessToken)
        {
            var requestUri = $"{_authServiceBaseUrl.TrimEnd('/')}/api/Auth/update-user";

            // Prepare the payload for auth-service. It expects UserUpdateRequest.
            // We only send fields that are relevant for this update or are non-null in our DTO.
            var authServicePayload = new AuthUserUpdateRequest
            {
                Username = string.IsNullOrEmpty(userProfileUpdate.Username) ? null : userProfileUpdate.Username,
                ProfileImage = userProfileUpdate.ProfileImageUrl
                // ProfileDescription and Location are not part of UserProfileUpdateDto,
                // so they will be null and auth-service will not update them unless explicitly sent.
            };

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(authServicePayload, options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    // Optionally, deserialize response if auth-service sends back useful info
                    // var responseContent = await response.Content.ReadAsStringAsync();
                    // Console.WriteLine($"Successfully updated user profile in auth-service: {responseContent}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Error updating user profile in auth-service: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during auth-service profile update: {ex.Message}");
                return false;
            }
        }
    }

    // This DTO matches the UserUpdateRequest expected by auth-service's AuthController
    internal class AuthUserUpdateRequest
    {
        public string? Username { get; set; }
        public string? ProfileImage { get; set; }
        public string? ProfileDescription { get; set; } // Included for completeness, will be null if not set
        public string? Location { get; set; } // Included for completeness, will be null if not set
    }
}
