using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class EscalationRule : BaseEntity
{
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public EscalationLevel EscalationLevel { get; set; }
    public int TriggerAfterHours { get; set; }
    public string? EscalateToUserId { get; set; }

    // Navigation
    public TicketCategory Category { get; set; } = null!;
}
