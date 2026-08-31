using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Tickets;

public class TicketEscalationDto
{
    public string Id { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public EscalationLevel EscalationLevel { get; set; }
    public string EscalatedToUserId { get; set; } = string.Empty;
    public string? EscalatedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
