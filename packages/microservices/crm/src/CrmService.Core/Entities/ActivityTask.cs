using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P7 — ACTIVITY_TASK. Scheduled follow-up tasks per client/opportunity; overdue when past
/// DueDate and still Open (worker flags). Dormant-client sweep auto-creates one for the Account Owner.</summary>
public class ActivityTask : BaseEntity
{
    public string? CustomerId { get; set; }
    public string? OpportunityId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string TaskType { get; set; } = "FollowUp";
    public string Subject { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public ActivityTaskStatus Status { get; set; } = ActivityTaskStatus.Open;
    public DateTime? CompletedAt { get; set; }
    public bool IsAutoCreated { get; set; }
    public DateTime? OverdueAlertedAt { get; set; }
}
