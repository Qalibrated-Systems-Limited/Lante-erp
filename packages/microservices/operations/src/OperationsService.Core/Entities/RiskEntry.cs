using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR3 — the RAID risk register. Originally assignment-only; a risk is now scoped to a project OR an
/// assignment (exactly one of the two), because the risks worth governing are usually about the
/// project as a whole and the old shape could not express one.
///
/// <para>Both links are kept rather than migrating assignment risks up to their project: an
/// assignment-level risk is a real thing a supervisor raises about one visit, and flattening those
/// into the project register would bury the project's own risks under site noise.</para>
/// </summary>
public class RiskEntry : BaseEntity
{
    public string? AssignmentId { get; set; }
    public string? ProjectId    { get; set; }

    public string  Title       { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;

    public RiskLikelihood Likelihood { get; set; } = RiskLikelihood.Low;
    public RiskImpact     Impact     { get; set; } = RiskImpact.Low;

    public string? Mitigation { get; set; }
    /// <summary>Free-text owner name, kept from the original shape for the assignment register.</summary>
    public string? Owner { get; set; }
    /// <summary>The person accountable for the mitigation. A mitigation with no owner is a wish.</summary>
    public string? OwnerUserId { get; set; }

    public RiskStatus Status { get; set; } = RiskStatus.Open;

    /// <summary>
    /// When the risk is next due to be re-assessed. A register nobody revisits goes stale within
    /// weeks, so the review date is what makes it a living document rather than a one-off list.
    /// </summary>
    public DateTime? ReviewDate { get; set; }

    /// <summary>Set when <see cref="Status"/> becomes Realised — the issue this risk turned into.</summary>
    public string? RealisedAsIssueId { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Likelihood x impact, 1–9. Stored rather than computed so the register can be sorted and
    /// filtered by severity in the database instead of pulling every row into memory to rank it.
    /// </summary>
    public int Score { get; set; }

    public Assignment? Assignment { get; set; }
    public Project?    Project    { get; set; }

    public static int ScoreOf(RiskLikelihood likelihood, RiskImpact impact) => (int)likelihood * (int)impact;
}
