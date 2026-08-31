using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H2 (HR-003, P3) — PROBATION_REVIEW. Every hire starts on probation; the scheduler raises a review at Day 90
/// and the confirmation decision at Day 180 (QSL policy is a 6-month probation), each as a
/// <see cref="ProbationOutcome.Pending"/> row that HR and the line manager then complete.
/// <para>The row itself is the alert record: <see cref="AlertSentAt"/> is stamped when the notification goes
/// out so a daily scheduler cannot re-fire it, and the row's existence is what stops the same milestone being
/// raised twice.</para>
/// <para>An <see cref="ProbationOutcome.Extended"/> outcome creates a follow-up review with new dates rather
/// than reopening this one, so the history of each decision survives.</para>
/// </summary>
public class ProbationReview : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public ProbationReviewType ReviewType { get; set; }
    /// <summary>The milestone date this review was due (hire date + 90 or 180 days).</summary>
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public ProbationOutcome Outcome { get; set; } = ProbationOutcome.Pending;
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? Notes { get; set; }

    /// <summary>When probation is extended, the date the follow-up review is due.</summary>
    public DateTime? ExtendedToDate { get; set; }
    /// <summary>The follow-up review created by an extension, for traceability.</summary>
    public string? FollowUpReviewId { get; set; }

    /// <summary>Stamped when the milestone notification was raised — prevents the daily sweep re-alerting.</summary>
    public DateTime? AlertSentAt { get; set; }

    public Employee? Employee { get; set; }
}
