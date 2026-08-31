namespace HrService.Core.DTOs.Probation;

// ── Probation (P3) ──
public class ProbationReviewDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string ReviewType { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public string? Notes { get; set; }
    public DateTime? ExtendedToDate { get; set; }
    public string? FollowUpReviewId { get; set; }
    public DateTime? AlertSentAt { get; set; }
    /// <summary>Still pending and its milestone date has passed.</summary>
    public bool IsOverdue { get; set; }
    public int DaysSinceScheduled { get; set; }
}

public class RecordProbationOutcomeDto
{
    /// <summary>Confirmed | Extended | Terminated</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    /// <summary>Required when extending — the date the follow-up review falls due.</summary>
    public DateTime? ExtendedToDate { get; set; }
}

// ── Contract renewal (P3 step 3.4 / P33) ──
public class ContractRenewalAlertDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime ContractEndDate { get; set; }
    public DateTime? Alert30SentAt { get; set; }
    public DateTime? Alert7SentAt { get; set; }
    public DateTime? ExpiredAlertSentAt { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ActionedBy { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Notes { get; set; }
    /// <summary>Days until expiry; negative once the contract has lapsed.</summary>
    public int DaysToExpiry { get; set; }
    public bool IsExpired { get; set; }
}

public class RenewContractDto
{
    /// <summary>The new contract end date. Must be later than the current one.</summary>
    public DateTime NewContractEndDate { get; set; }
    public string? Notes { get; set; }
}

public class ConvertToPermanentDto
{
    public string? Notes { get; set; }
}

public class LetContractExpireDto
{
    public string? Notes { get; set; }
}

// ── Summary / results ──
public class ProbationSummaryDto
{
    public int OnProbation { get; set; }
    public int ReviewsPending { get; set; }
    public int ReviewsOverdue { get; set; }
    public int ConfirmedThisYear { get; set; }
    public int ExtendedThisYear { get; set; }
    public int TerminationRecommended { get; set; }

    public int FixedTermStaff { get; set; }
    public int ContractsExpiringIn30Days { get; set; }
    public int ContractsExpiringIn7Days { get; set; }
    public int ContractsExpiredUnactioned { get; set; }
    public int AwaitingRenewalDecision { get; set; }
}

public record ProbationActionResult(string Status, string Message, string? Id = null);

/// <summary>What a sweep did — returned by the manual trigger and logged by the scheduler.</summary>
public class MilestoneSweepResultDto
{
    public int ProbationReviewsRaised { get; set; }
    /// <summary>Follow-up reviews from an extension that have now reached their date.</summary>
    public int FollowUpReviewsAlerted { get; set; }
    public int ContractAlertsOpened { get; set; }
    public int Alert30Fired { get; set; }
    public int Alert7Fired { get; set; }
    public int ExpiredFlagged { get; set; }
    public string Message { get; set; } = string.Empty;
}
