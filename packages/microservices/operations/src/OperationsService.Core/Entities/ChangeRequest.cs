using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR3 — a controlled change to an approved baseline. Without this, PR1's baseline is just another
/// field anyone can overwrite, and PR4's variance would measure against a moving target.
///
/// <para><b>Cost impact goes through a budget version, not a number typed here.</b> A schedule-only
/// change needs no budget version; a change that costs money must link a Draft one, and approving the
/// request approves that version. That keeps PR1's rule — approving a budget version IS setting the
/// baseline — as the single mechanism, so the change request and the budget can never disagree about
/// the figure.</para>
/// </summary>
public class ChangeRequest : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;

    public string Number      { get; set; } = string.Empty;   // CR-2026-0001
    public string Title       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Why the change is needed. Required — an unjustified baseline move is the thing being prevented.</summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Days to shift every milestone baseline by on approval. Positive slips the plan, negative pulls
    /// it in, zero means the change costs money but no time.
    /// </summary>
    public int ScheduleImpactDays { get; set; }

    /// <summary>The priced budget this change carries, if it has a cost. Null for schedule-only changes.</summary>
    public string? BudgetVersionId { get; set; }

    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Draft;

    public string?   RequestedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string?   DecidedBy   { get; set; }
    public DateTime? DecidedAt   { get; set; }
    public string?   DecisionReason { get; set; }

    // ── The baseline as it stood before approval ────────────────────────────────
    // Snapshotted on the request itself, not only in ProjectHistory. History is a narrative log; this
    // is the structured "what did we agree before this change" that a variance report can read back
    // without parsing prose.

    public decimal?  PreviousBaselineBudget { get; set; }
    public DateTime? PreviousBaselineSetAt  { get; set; }
    /// <summary>JSON array of {milestoneId, baselineStart, baselineDue} as they were before the shift.</summary>
    public string? PreviousMilestoneBaselines { get; set; }

    public decimal? NewBaselineBudget { get; set; }
    public int      MilestonesShifted { get; set; }

    public Project Project { get; set; } = null!;
}
