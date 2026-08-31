using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Services;

public interface INotificationService
{
    Task NotifyAssignedAsync(Ticket ticket, string assignedToUserId, string assignedByUserId);
    Task NotifyStatusChangedAsync(Ticket ticket, string oldStatus, string newStatus, string changedByUserId);
    Task NotifyCommentAddedAsync(Ticket ticket, string commentAuthorUserId, string commentPreview);
    Task NotifyEscalatedAsync(Ticket ticket, string escalatedByUserId);
    Task NotifyResolvedAsync(Ticket ticket, string resolvedByUserId);
    Task NotifyWatchersAsync(Ticket ticket, string eventType, string message, string changedByUserId);

    // Returns the created notification's Id — callers that need to track delivery/read status
    // downstream (e.g. the platform broadcast "who hasn't read this" view) capture it.
    Task<string> SendAsync(string userId, string type, string message, string? ticketId = null);

    Task<IEnumerable<Notification>> GetForUserAsync(string userId, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync(string userId);
    Task<Notification?> GetByIdAsync(string id);
    Task MarkReadAsync(string notificationId, string userId);
    Task MarkUnreadAsync(string notificationId, string userId);
    Task MarkAllReadAsync(string userId);
}
