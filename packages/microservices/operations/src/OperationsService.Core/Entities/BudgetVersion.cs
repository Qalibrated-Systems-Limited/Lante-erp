using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR1 — a detailed project budget, submitted for approval as a whole.
///
/// Budgets are versioned rather than edited in place. The previously approved version is marked
/// Superseded and kept, so "what did we originally approve?" stays answerable after a revision —
/// which is the question the old single overwritable <c>Project.PlannedBudget</c> could not answer.
/// Exactly one version per project is Approved at a time, and that version is the baseline every
/// variance figure is measured against.
/// </summary>
public class BudgetVersion : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>1-based, per project. Shown to users as "Budget v3".</summary>
    public int VersionNo { get; set; }

    public BudgetVersionStatus Status { get; set; } = BudgetVersionStatus.Draft;

    /// <summary>Why this revision was raised. Required from v2 onward — a budget change without a
    /// stated reason is exactly what the approval step exists to prevent.</summary>
    public string? RevisionReason { get; set; }

    // Rolled up from the lines on submit, so approvals and reports do not each re-sum them.
    public decimal TotalPlanned { get; set; }
    /// <summary>Sum of the lines' quoted amounts — what the client is being charged for this scope.</summary>
    public decimal TotalQuoted  { get; set; }

    public string?   SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string?   ApprovedBy  { get; set; }
    public DateTime? ApprovedAt  { get; set; }
    public string?   RejectedBy  { get; set; }
    public DateTime? RejectedAt  { get; set; }
    public string?   RejectionReason { get; set; }

    /// <summary>Set when a later version is approved in its place.</summary>
    public DateTime? SupersededAt { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<BudgetLine> Lines { get; set; } = new List<BudgetLine>();
}
