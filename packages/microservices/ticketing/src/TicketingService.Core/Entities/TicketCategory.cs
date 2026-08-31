using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class TicketCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string DepartmentId { get; set; } = string.Empty;
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;
    public string? DefaultAssigneeId { get; set; }
    public bool RequiresEvidence { get; set; } = false;
    public bool AutoCreateANCR { get; set; } = false;
    /// D1-5 — marks the complaint category so the complaint 5-step workflow (D5) and 30-day repeat
    /// detection (D4-3) can key off it without hardcoding a category id.
    public bool IsComplaint { get; set; } = false;
    /// D1-5 — clock behaviour. True (default) = SLA hours are business hours (nights/weekends skipped,
    /// pausable). False = 24/7 wall-clock, never paused — Emergency & IT-P1 per spec.
    public bool BusinessHoursOnly { get; set; } = true;

    // Navigations
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<SLAPolicy> SLAPolicies { get; set; } = new List<SLAPolicy>();
    public ICollection<EscalationRule> EscalationRules { get; set; } = new List<EscalationRule>();
}
