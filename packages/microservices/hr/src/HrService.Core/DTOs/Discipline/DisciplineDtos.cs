namespace HrService.Core.DTOs.Discipline;

/// <summary>The result shape every H10 write returns, matching H5–H9.</summary>
public record DisciplineActionResult(string Status, string Message, string? Id = null)
{
    public List<string> Warnings { get; init; } = [];
}

// ── Disciplinary cases (P18) ──
public class DisciplinaryCaseDto
{
    public string Id { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Witnesses { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceReference { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime? ShowCauseIssuedAt { get; set; }
    public string? ShowCauseLetter { get; set; }
    public DateTime? ResponseDeadline { get; set; }
    public DateTime? EmployeeRespondedAt { get; set; }
    public string? EmployeeResponse { get; set; }

    public DateTime? HearingDate { get; set; }
    public string? HearingPanel { get; set; }
    public string? HearingNotes { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? OutcomeNotes { get; set; }
    public DateTime? OutcomeRecordedAt { get; set; }

    public DateTime? RightOfAppealDeadline { get; set; }
    public DateTime? AppealSubmittedAt { get; set; }
    public string? AppealGrounds { get; set; }
    public string? AppealOutcome { get; set; }
    public DateTime? ClosedAt { get; set; }

    public string? WarningRecordId { get; set; }
    public string? SeparationId { get; set; }
    /// <summary>What has to happen next, in words.</summary>
    public string NextStep { get; set; } = string.Empty;
    /// <summary>True once the show-cause window has lapsed with no response.</summary>
    public bool ResponseOverdue { get; set; }
}

public class OpenCaseDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Witnesses { get; set; }
    /// <summary>"Operations" for a negligence incident, "HR" for a failed improvement plan.</summary>
    public string? SourceModule { get; set; }
    public string? SourceReference { get; set; }
}

public class IssueShowCauseDto
{
    public string? Letter { get; set; }
    /// <summary>Working days to respond. Defaults to the policy's five.</summary>
    public int? ResponseDays { get; set; }
}

public class RecordResponseDto
{
    public string? Response { get; set; }
    public DateTime? HearingDate { get; set; }
    public string? HearingPanel { get; set; }
}

public class RecordOutcomeDto
{
    /// <summary>NoAction | Warning | Suspension | Termination.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? HearingNotes { get; set; }
    /// <summary>Verbal | Written | FinalWritten — required when the outcome is a warning.</summary>
    public string? WarningType { get; set; }
    /// <summary>Days to appeal. Defaults to the policy's fourteen.</summary>
    public int? AppealDays { get; set; }
}

public class RecordAppealDto
{
    public string? Grounds { get; set; }
    /// <summary>Left blank when only lodging the appeal; set when deciding it.</summary>
    public string? Decision { get; set; }
}

// ── Warnings (P19) ──
public class WarningRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DisciplinaryCaseId { get; set; }
    public string WarningType { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
    public bool AcknowledgedByEmployee { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int DaysToExpiry { get; set; }
    /// <summary>Live warnings this employee has in the rolling twelve months — three is a review trigger.</summary>
    public int ActiveInLast12Months { get; set; }
}

public class IssueWarningDto
{
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>Verbal | Written | FinalWritten.</summary>
    public string WarningType { get; set; } = "Verbal";
    public DateTime? IncidentDate { get; set; }
    public string? Reason { get; set; }
    public string? DisciplinaryCaseId { get; set; }
}

public class AcknowledgeWarningDto
{
    public string? Note { get; set; }
}

// ── Grievances (P20) ──
public class GrievanceCaseDto
{
    public string Id { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? AcknowledgementDeadline { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AssignedToEmployeeId { get; set; }
    public string? AssignedToName { get; set; }
    public string? InvestigationFindings { get; set; }
    public string? Outcome { get; set; }
    public string? FollowUpActions { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    /// <summary>True once the two-working-day acknowledgement window has lapsed.</summary>
    public bool SlaBreached { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class SubmitGrievanceDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
}

public class AssignGrievanceDto
{
    public string InvestigatorEmployeeId { get; set; } = string.Empty;
}

public class ResolveGrievanceDto
{
    /// <summary>Resolved | PartiallyResolved | Escalated.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? Findings { get; set; }
    public string? OutcomeNotes { get; set; }
    public string? FollowUpActions { get; set; }
}

// ── Separation and final dues (P21) ──
public class SeparationDto
{
    public string Id { get; set; } = string.Empty;
    public string SeparationNumber { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime HireDate { get; set; }
    public string SeparationType { get; set; } = string.Empty;
    public DateTime NoticeDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public int NoticePeriodDays { get; set; }
    public bool NoticeWaived { get; set; }
    public string? Reason { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceReference { get; set; }

    public decimal MonthlySalary { get; set; }
    public decimal DailyRate { get; set; }
    public decimal LeaveDaysBalance { get; set; }
    public decimal LeavePayout { get; set; }
    public int FinalMonthDaysWorked { get; set; }
    public decimal ProRataSalary { get; set; }
    public decimal NoticePay { get; set; }
    public decimal OtherEarnings { get; set; }
    public decimal AdvanceRecovery { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal NetDues { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string? DuesNotes { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }
    public string? ExitInterviewNotes { get; set; }
    public string? CancellationReason { get; set; }
    public int YearsOfService { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class InitiateSeparationDto
{
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>Resignation | Termination | Retirement | EndOfContract | Death.</summary>
    public string SeparationType { get; set; } = "Resignation";
    public DateTime? NoticeDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public int? NoticePeriodDays { get; set; }
    public bool NoticeWaived { get; set; }
    public string? Reason { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceReference { get; set; }
}

public class AdjustDuesDto
{
    public decimal? OtherEarnings { get; set; }
    /// <summary>Outstanding advances recovered. Pre-filled from finance where it can be read.</summary>
    public decimal? AdvanceRecovery { get; set; }
    public decimal? OtherDeductions { get; set; }
    public string? Notes { get; set; }
    public string? ExitInterviewNotes { get; set; }
}

public class DecideSeparationDto
{
    /// <summary>Approve | Cancel.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class PaySeparationDto
{
    public string? PaymentReference { get; set; }
    /// <summary>P21 step 21.6 — record that the Certificate of Service has been issued.</summary>
    public bool IssueCertificate { get; set; } = true;
}

// ── Summary and sweep ──
public class DisciplineSummaryDto
{
    public int OpenCases { get; set; }
    public int AwaitingResponse { get; set; }
    public int ResponseOverdue { get; set; }
    public int AwaitingHearing { get; set; }
    public int UnderAppeal { get; set; }

    public int ActiveWarnings { get; set; }
    public int UnacknowledgedWarnings { get; set; }
    /// <summary>Employees with three or more live warnings in the rolling twelve months.</summary>
    public int EmployeesAtReviewThreshold { get; set; }

    public int OpenGrievances { get; set; }
    public int GrievancesAwaitingAcknowledgement { get; set; }
    public int GrievanceSlaBreaches { get; set; }
    public int GrievancesEscalated { get; set; }

    public int SeparationsInProgress { get; set; }
    public int SeparationsAwaitingMd { get; set; }
    public int SeparationsAwaitingPayment { get; set; }
    public decimal DuesOutstanding { get; set; }
}

public class DisciplineSweepResultDto
{
    public int WarningsExpired { get; set; }
    public int WarningEscalations { get; set; }
    public int ShowCauseOverdueAlerts { get; set; }
    public int GrievanceSlaBreaches { get; set; }
    public List<string> Notes { get; set; } = [];
}
