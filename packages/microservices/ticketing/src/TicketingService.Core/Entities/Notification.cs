namespace TicketingService.Core.Entities;

public class Notification : BaseEntity
{
    /// <summary>The user this notification is for (userId from user-service)</summary>
    public string UserId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;   // assigned, status_changed, comment_added, escalated, resolved
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Link to the relevant ticket</summary>
    public string? TicketId { get; set; }
    public string? TicketTitle { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }

    // Navigation
    public Ticket? Ticket { get; set; }
}
