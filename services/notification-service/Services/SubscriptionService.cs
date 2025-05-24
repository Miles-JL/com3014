using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using notification_service.Data;
using notification_service.Models;
using notification_service.Models.Dtos;

namespace notification_service.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            AppDbContext context,
            ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task AddSubscriptionAsync(int userId, PushSubscriptionDto subscriptionDto)
        {
            var existing = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == subscriptionDto.Endpoint);

            if (existing != null)
            {
                // Update existing subscription
                existing.Auth = subscriptionDto.Keys.Auth;
                existing.P256Dh = subscriptionDto.Keys.P256Dh;
                existing.ExpiresAt = subscriptionDto.ExpirationTime;
            }
            else
            {
                // Add new subscription
                var subscription = new PushSubscription
                {
                    UserId = userId.ToString(),
                    Endpoint = subscriptionDto.Endpoint,
                    P256Dh = subscriptionDto.Keys.P256Dh,
                    Auth = subscriptionDto.Keys.Auth,
                    ExpiresAt = subscriptionDto.ExpirationTime,
                    CreatedAt = DateTime.UtcNow
                };
                
                await _context.PushSubscriptions.AddAsync(subscription);
            }
            
            await _context.SaveChangesAsync();
        }

        public async Task RemoveSubscriptionAsync(int userId, string endpoint)
        {
            var subscription = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId.ToString());
                
            if (subscription != null)
            {
                _context.PushSubscriptions.Remove(subscription);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<PushSubscription>> GetUserSubscriptions(int userId)
        {
            return await _context.PushSubscriptions
                .Where(s => s.UserId == userId.ToString() && 
                          (!s.ExpiresAt.HasValue || s.ExpiresAt > DateTime.UtcNow))
                .ToListAsync();
        }
    }
}
