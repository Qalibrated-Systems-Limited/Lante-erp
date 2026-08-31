using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class TicketEscalation : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public EscalationLevel EscalationLevel { get; set; }
    public string EscalatedToUserId { get; set; } = string.Empty;
    public string? EscalatedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; set; } = false;
    public DateTime? AcknowledgedAt { get; set; }

    // Navigation
    public Ticket Ticket { get; set; } = null!;
}
