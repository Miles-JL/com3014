using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;
using notification_service.Models;
using notification_service.Models.Dtos;

namespace notification_service.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly ILogger<PushNotificationService> _logger;
        private readonly VapidDetails _vapidDetails;
        private readonly ISubscriptionService _subscriptionService;

        public PushNotificationService(
            IConfiguration configuration,
            ILogger<PushNotificationService> logger,
            ISubscriptionService subscriptionService)
        {
            _logger = logger;
            _subscriptionService = subscriptionService;
            
            var publicKey = configuration["Vapid:PublicKey"] ?? 
                throw new ArgumentNullException("Vapid:PublicKey is not configured");
            var privateKey = configuration["Vapid:PrivateKey"] ?? 
                throw new ArgumentNullException("Vapid:PrivateKey is not configured");
            
            _vapidDetails = new VapidDetails(
                subject: configuration["Vapid:Subject"] ?? "mailto:your-email@example.com",
                publicKey: publicKey,
                privateKey: privateKey);
        }

        public async Task SendNotificationToUser(int userId, string title, string message, string? url = null)
        {
            try
            {
                var subscriptions = await _subscriptionService.GetUserSubscriptions(userId);
                foreach (var subscription in subscriptions)
                {
                    await SendNotificationAsync(new PushSubscriptionDto
                    {
                        Endpoint = subscription.Endpoint,
                        ExpirationTime = subscription.ExpiresAt,
                        Keys = new PushSubscriptionKeys
                        {
                            P256Dh = subscription.P256Dh,
                            Auth = subscription.Auth
                        }
                    }, new NotificationDto
                    {
                        Title = title,
                        Body = message,
                        Url = url ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}", userId);
                throw;
            }
        }

        public async Task SendNotificationAsync(PushSubscriptionDto subscription, NotificationDto notification)
        {
            try
            {
                var pushSubscription = new WebPush.PushSubscription(
                    subscription.Endpoint,
                    subscription.Keys.P256Dh,
                    subscription.Keys.Auth
                );

                var payload = JsonSerializer.Serialize(new
                {
                    title = notification.Title,
                    body = notification.Body,
                    url = notification.Url,
                    icon = notification.Icon
                });

                using (var webPushClient = new WebPushClient())
                {
                    await webPushClient.SendNotificationAsync(pushSubscription, payload, _vapidDetails);
                }
            }
            catch (WebPushException ex) when (ex.Message.Contains("Subscription no longer valid"))
            {
                _logger.LogWarning("Subscription no longer valid, removing: {Endpoint}", subscription.Endpoint);
                
                // Get the user ID from the subscription if possible, or log an error
                var userId = 0;
                try
                {
                    var subscriptions = await _subscriptionService.GetUserSubscriptions(0);
                    var sub = subscriptions.FirstOrDefault(s => s.Endpoint == subscription.Endpoint);
                    if (sub != null && int.TryParse(sub.UserId, out var id))
                    {
                        userId = id;
                    }
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error getting user ID for subscription {Endpoint}", subscription.Endpoint);
                }

                await _subscriptionService.RemoveSubscriptionAsync(userId, subscription.Endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to {Endpoint}", subscription.Endpoint);
                throw;
            }
        }
    }
}