using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class SLAPolicy : BaseEntity
{
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public int ResponseTimeHours { get; set; }
    public int ResolutionTimeHours { get; set; }
    /// D2-1/D2-2 — % of the resolution window that must elapse before an amber ("approaching SLA")
    /// alert fires (the third state between on-track and breached). Default 75.
    public int AmberThresholdPct { get; set; } = 75;
    /// D2-1 — optional branch this policy applies to (per-branch SLA); null = applies tenant-wide.
    public string? BranchId { get; set; }

    // Navigation
    public TicketCategory Category { get; set; } = null!;
}
