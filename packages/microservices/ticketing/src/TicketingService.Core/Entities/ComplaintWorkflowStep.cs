using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

/// D5 — one step in a complaint's 5-step handling workflow (HELP-007). Auto-created (5 rows) when a
/// ticket in an IsComplaint category is raised. Steps are completed strictly in order; each records
/// who completed it and when, plus step-specific fields (satisfaction outcome + sign-off on step 4,
/// root cause + preventive action on step 5).
public class ComplaintWorkflowStep : BaseEntity
{
    public string TicketId { get; set; } = string.Empty;
    public int StepNumber { get; set; }                 // 1..5, the fixed order
    public ComplaintStepType StepType { get; set; }
    public ComplaintStepStatus Status { get; set; } = ComplaintStepStatus.Pending;

    public string? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    // Step 4 — satisfaction check.
    public bool? SatisfactionMet { get; set; }
    /// Set when a dissatisfied (SatisfactionMet == false) step 4 required an MD/Dept-Head sign-off.
    public string? SignOffByUserId { get; set; }
    public DateTime? SignOffAt { get; set; }

    // Step 5 — close.
    public string? RootCause { get; set; }
    public string? PreventiveAction { get; set; }

    public Ticket? Ticket { get; set; }
}
