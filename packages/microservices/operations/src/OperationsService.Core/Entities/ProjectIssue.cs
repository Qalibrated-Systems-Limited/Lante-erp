using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR3 — the RAID issue log. Deliberately a separate entity from <see cref="RiskEntry"/>: a risk is a
/// maybe and is scored on likelihood, an issue has already happened and likelihood is meaningless for
/// it. Folding both into one table with half the columns null for each kind would make every query
/// ask "which sort of row is this?" before it could do anything useful.
/// </summary>
public class ProjectIssue : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Optional — an issue that is confined to one milestone rather than the whole project.</summary>
    public string? MilestoneId { get; set; }

    public string Number      { get; set; } = string.Empty;   // ISS-2026-0001
    public string Title       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;
    public IssueStatus   Status   { get; set; } = IssueStatus.Open;

    public string? OwnerUserId { get; set; }

    public DateTime  RaisedAt { get; set; } = DateTime.UtcNow;
    public string?   RaisedBy { get; set; }

    /// <summary>When it needs to be resolved by. Drives the overdue flag on the log.</summary>
    public DateTime? TargetResolutionDate { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public string?   ResolvedBy { get; set; }
    public string?   Resolution { get; set; }

    /// <summary>
    /// Set when the issue was raised by a risk materialising. Keeping the link is the point of having
    /// both logs: it is the evidence that the register was doing its job, or that it missed this one.
    /// </summary>
    public string? RaisedFromRiskId { get; set; }

    public Project Project { get; set; } = null!;
}
