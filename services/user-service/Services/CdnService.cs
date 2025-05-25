using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace UserService.Services
{
    public class CdnService : ICdnService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cdnServiceBaseUrl;
        private readonly ILogger _logger;

        public CdnService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<CdnService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("CdnServiceClient");
            _cdnServiceBaseUrl = configuration["ServiceUrls:CdnService"] ?? "http://cdn-service:5250";
            _logger = logger;
        }

        public async Task<string?> UploadProfileImageAsync(IFormFile file, string accessToken)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var requestUri = $"{_cdnServiceBaseUrl.TrimEnd('/')}/api/upload";

            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(file.OpenReadStream());
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "file", file.FileName);

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    var cdnResponse = await JsonSerializer.DeserializeAsync<CdnUploadResponse>(responseStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return cdnResponse?.FileUrl;
                }
                else
                {
                    // Log error (e.g., response.StatusCode, await response.Content.ReadAsStringAsync())
                    Console.WriteLine($"Error uploading to CDN: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"Exception during CDN upload: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteProfileImageAsync(string fileName, string accessToken)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning("DeleteProfileImageAsync called with null or empty fileName.");
                return false;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                // The cdn-service expects just the filename (e.g., guid.jpg) not the full path or URL part
                var justFileName = Path.GetFileName(fileName); // Extracts filename from URL or path

                _logger.LogInformation("Attempting to delete image {FileName} from CDN.", justFileName);
                var response = await _httpClient.DeleteAsync($"{_cdnServiceBaseUrl.TrimEnd('/')}/api/upload/{justFileName}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully deleted image {FileName} from CDN.", justFileName);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete image {FileName} from CDN. Status: {StatusCode}, Error: {Error}", 
                                     justFileName, response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while trying to delete image {FileName} from CDN.", fileName);
                return false;
            }
        }
    }

    internal class CdnUploadResponse
    {
        public string? FileUrl { get; set; }
    }
}
