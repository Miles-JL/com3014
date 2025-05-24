using System.Collections.Generic;
using System.Threading.Tasks;
using notification_service.Models;
using notification_service.Models.Dtos;

namespace notification_service.Services
{
    public interface ISubscriptionService
    {
        Task AddSubscriptionAsync(int userId, PushSubscriptionDto subscription);
        Task RemoveSubscriptionAsync(int userId, string endpoint);
        Task<IEnumerable<PushSubscription>> GetUserSubscriptions(int userId);
    }
}
