namespace OperationsService.Core.DTOs.Governance;

// PR3 — RAID + change control. Enums cross the wire as strings: the UI renders them directly and a
// stored ordinal leaking into JSON would make the API unreadable and brittle to reordering.

// ── Risks ───────────────────────────────────────────────────────────────────────

public class ProjectRiskDto
{
    public string  Id          { get; set; } = string.Empty;
    public string? ProjectId   { get; set; }
    public string? AssignmentId{ get; set; }
    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string  Likelihood  { get; set; } = string.Empty;
    public string  Impact      { get; set; } = string.Empty;
    public int     Score       { get; set; }
    /// <summary>Low / Medium / High / Critical, derived from the score for a consistent colour band.</summary>
    public string  Severity    { get; set; } = string.Empty;
    public string? Mitigation  { get; set; }
    public string? Owner       { get; set; }
    public string? OwnerUserId { get; set; }
    public string  Status      { get; set; } = string.Empty;
    public DateTime? ReviewDate { get; set; }
    /// <summary>True when the review date has passed and the risk is still live.</summary>
    public bool    ReviewOverdue { get; set; }
    public string? RealisedAsIssueId { get; set; }
    public DateTime? ClosedAt  { get; set; }
    public DateTime  CreatedAt { get; set; }
}

public class UpsertProjectRiskDto
{
    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string? Likelihood  { get; set; }
    public string? Impact      { get; set; }
    public string? Mitigation  { get; set; }
    public string? Owner       { get; set; }
    public string? OwnerUserId { get; set; }
    public string? Status      { get; set; }
    public DateTime? ReviewDate { get; set; }
}

/// <summary>Turning a live risk into an issue. The issue inherits the risk's title unless overridden.</summary>
public class RealiseRiskDto
{
    public string? Title       { get; set; }
    public string? Description { get; set; }
    public string? Severity    { get; set; }
    public string? MilestoneId { get; set; }
    public string? OwnerUserId { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
}

// ── Issues ──────────────────────────────────────────────────────────────────────

public class ProjectIssueDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  ProjectId   { get; set; } = string.Empty;
    public string? MilestoneId { get; set; }
    public string  Number      { get; set; } = string.Empty;
    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string  Severity    { get; set; } = string.Empty;
    public string  Status      { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public DateTime  RaisedAt  { get; set; }
    public string?   RaisedBy  { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    /// <summary>True when the target date has passed and the issue is still unresolved.</summary>
    public bool      Overdue    { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string?   ResolvedBy { get; set; }
    public string?   Resolution { get; set; }
    public string?   RaisedFromRiskId { get; set; }
    public DateTime  CreatedAt  { get; set; }
}

public class UpsertProjectIssueDto
{
    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string? MilestoneId { get; set; }
    public string? Severity    { get; set; }
    public string? Status      { get; set; }
    public string? OwnerUserId { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
}

public class ResolveIssueDto
{
    public string Resolution { get; set; } = string.Empty;
    /// <summary>Close it outright rather than leaving it Resolved pending confirmation.</summary>
    public bool   Close      { get; set; }
}

// ── Change requests ─────────────────────────────────────────────────────────────

public class ChangeRequestDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  ProjectId   { get; set; } = string.Empty;
    public string  Number      { get; set; } = string.Empty;
    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string  Justification { get; set; } = string.Empty;
    public int     ScheduleImpactDays { get; set; }
    public string? BudgetVersionId    { get; set; }
    public int?    BudgetVersionNo    { get; set; }
    public decimal? BudgetVersionTotal { get; set; }
    public string  Status      { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string?   DecidedBy   { get; set; }
    public DateTime? DecidedAt   { get; set; }
    public string?   DecisionReason { get; set; }
    public decimal?  PreviousBaselineBudget { get; set; }
    public DateTime? PreviousBaselineSetAt  { get; set; }
    public decimal?  NewBaselineBudget      { get; set; }
    public int       MilestonesShifted      { get; set; }
    public DateTime  CreatedAt { get; set; }
}

public class UpsertChangeRequestDto
{
    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string  Justification { get; set; } = string.Empty;
    public int     ScheduleImpactDays { get; set; }
    public string? BudgetVersionId    { get; set; }
}

public class DecideChangeRequestDto
{
    public bool    Approved { get; set; }
    public string? Reason   { get; set; }
}

/// <summary>Counts for the governance tab header — what needs attention without loading every row.</summary>
public class GovernanceSummaryDto
{
    public int OpenRisks       { get; set; }
    public int HighRisks       { get; set; }
    public int RisksOverdueReview { get; set; }
    public int OpenIssues      { get; set; }
    public int OverdueIssues   { get; set; }
    public int PendingChangeRequests { get; set; }
}
