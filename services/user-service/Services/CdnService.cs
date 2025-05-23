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

        public CdnService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("CdnServiceClient");
            _cdnServiceBaseUrl = configuration["ServiceUrls:CdnService"] ?? "http://localhost:5250";
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
    }

    internal class CdnUploadResponse
    {
        public string? FileUrl { get; set; }
    }
}
