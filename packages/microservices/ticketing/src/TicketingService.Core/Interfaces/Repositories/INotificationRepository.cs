using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetForUserAsync(string userId, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync(string userId);
    Task AddAsync(Notification notification);
    Task AddRangeAsync(IEnumerable<Notification> notifications);
    Task MarkReadAsync(string notificationId, string userId);
    Task MarkUnreadAsync(string notificationId, string userId);
    Task MarkAllReadAsync(string userId);
    Task<Notification?> GetByIdAsync(string id);
}
