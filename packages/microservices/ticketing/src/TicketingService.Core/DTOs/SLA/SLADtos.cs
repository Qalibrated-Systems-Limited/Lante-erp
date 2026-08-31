using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.SLA;

public class SLAPolicyReadDto
{
    public string Id { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public int ResponseTimeHours { get; set; }
    public int ResolutionTimeHours { get; set; }
    public int AmberThresholdPct { get; set; }
    public string? BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSLAPolicyDto
{
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public int ResponseTimeHours { get; set; }
    public int ResolutionTimeHours { get; set; }
    public int AmberThresholdPct { get; set; } = 75;
    public string? BranchId { get; set; }
}

public class EscalationRuleReadDto
{
    public string Id { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public EscalationLevel EscalationLevel { get; set; }
    public int TriggerAfterHours { get; set; }
    public string? EscalateToUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEscalationRuleDto
{
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public EscalationLevel EscalationLevel { get; set; }
    public int TriggerAfterHours { get; set; }
    public string? EscalateToUserId { get; set; }
}

public class SLADeadlines
{
    public DateTime? ResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
}

public class SLASummaryDto
{
    public int TotalTickets { get; set; }
    public int ResponseBreached { get; set; }
    public int ResolutionBreached { get; set; }
    public int OnTrack { get; set; }
    public double ComplianceRate { get; set; }
}
