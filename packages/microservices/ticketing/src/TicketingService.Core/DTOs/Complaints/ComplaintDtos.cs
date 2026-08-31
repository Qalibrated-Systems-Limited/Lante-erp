using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Complaints;

public class ComplaintStepReadDto
{
    public string Id { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public ComplaintStepType StepType { get; set; }
    public string StepTypeLabel => StepType.ToString();
    public ComplaintStepStatus Status { get; set; }
    public string StatusLabel => Status.ToString();
    public string? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public bool? SatisfactionMet { get; set; }
    public string? SignOffByUserId { get; set; }
    public DateTime? SignOffAt { get; set; }
    public string? RootCause { get; set; }
    public string? PreventiveAction { get; set; }
}

/// D5 — payload to complete the current active step. Only the fields relevant to that step are used
/// (satisfaction + sign-off on step 4; root cause + preventive action on step 5; notes otherwise).
public class CompleteComplaintStepDto
{
    public string? Notes { get; set; }
    public bool? SatisfactionMet { get; set; }
    public string? SignOffByUserId { get; set; }
    public string? RootCause { get; set; }
    public string? PreventiveAction { get; set; }
}
