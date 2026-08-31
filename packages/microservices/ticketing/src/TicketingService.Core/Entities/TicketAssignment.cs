namespace TicketingService.Core.Entities;

public class TicketAssignment : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public string AssignedToUserId { get; set; } = string.Empty;
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; } = true;

    // Navigation
    public Ticket Ticket { get; set; } = null!;
}
