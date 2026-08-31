using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class NotificationRepository(TicketingDbContext context) : INotificationRepository
{
    public async Task<IEnumerable<Notification>> GetForUserAsync(string userId, bool unreadOnly = false)
    {
        var query = context.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
        => await context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted);

    public async Task AddAsync(Notification notification)
    {
        await context.Notifications.AddAsync(notification);
        await context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications)
    {
        await context.Notifications.AddRangeAsync(notifications);
        await context.SaveChangesAsync();
    }

    public async Task MarkReadAsync(string notificationId, string userId)
    {
        var n = await context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n == null) return;
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        n.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task MarkUnreadAsync(string notificationId, string userId)
    {
        var n = await context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n == null) return;
        n.IsRead = false;
        n.ReadAt = null;
        n.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var unread = await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
            n.UpdatedAt = now;
        }
        await context.SaveChangesAsync();
    }

    public async Task<Notification?> GetByIdAsync(string id)
        => await context.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
}
