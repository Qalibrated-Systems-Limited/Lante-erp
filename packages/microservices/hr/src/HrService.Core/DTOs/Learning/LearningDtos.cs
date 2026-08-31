namespace HrService.Core.DTOs.Learning;

/// <summary>The result shape every H7 write returns, matching H5/H6.</summary>
public record LearningActionResult(string Status, string Message, string? Id = null)
{
    public List<string> Warnings { get; init; } = [];
}

// ── Learning & development plans (P22) ──
public class LdpObjectiveDto
{
    public string Id { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string? Activity { get; set; }
    public DateTime? TargetDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletionDate { get; set; }
    public string? CompletedByTrainingId { get; set; }
    public string? Notes { get; set; }
    public bool IsOverdue { get; set; }
}

public class LdpDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public int PlanYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public string? Notes { get; set; }
    public List<LdpObjectiveDto> Objectives { get; set; } = [];
    public int ObjectivesCompleted { get; set; }
}

public class SaveLdpDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public int? PlanYear { get; set; }
    public string? Notes { get; set; }
    public List<SaveLdpObjectiveDto> Objectives { get; set; } = [];
}

public class SaveLdpObjectiveDto
{
    public string? Id { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string? Activity { get; set; }
    public DateTime? TargetDate { get; set; }
    public string? Notes { get; set; }
}

public class DecideLdpDto
{
    /// <summary>Approve | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

// ── Training events and hours (P23) ──
public class TrainingEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime TrainingDate { get; set; }
    public decimal DurationHours { get; set; }
    public decimal Cost { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? MandatoryTrainingCode { get; set; }
    public string? CertificateUrl { get; set; }
    public string? KnowledgeSharingSessionId { get; set; }
    public int AttendeeCount { get; set; }
    public decimal TotalHoursAwarded { get; set; }
    public List<TrainingAttendeeDto> Attendees { get; set; } = [];
}

public class TrainingAttendeeDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public decimal Hours { get; set; }
    public DateTime TrainingDate { get; set; }
    public string? TrainingTitle { get; set; }
    public string? LdpObjectiveId { get; set; }
    public string? VerifiedBy { get; set; }
}

public class SaveTrainingEventDto
{
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Description { get; set; }
    /// <summary>External | Internal | KnowledgeSharing | OnTheJob.</summary>
    public string Source { get; set; } = "External";
    public DateTime TrainingDate { get; set; }
    public decimal DurationHours { get; set; }
    public decimal Cost { get; set; }
    public string? DepartmentId { get; set; }
    /// <summary>Set when the event satisfies a mandatory requirement, e.g. DATA_PROTECTION.</summary>
    public string? MandatoryTrainingCode { get; set; }
    public string? CertificateUrl { get; set; }
    /// <summary>Attendees; each gets the event's hours unless overridden.</summary>
    public List<TrainingAttendeeInputDto> Attendees { get; set; } = [];
}

public class TrainingAttendeeInputDto
{
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>Defaults to the event's duration when omitted.</summary>
    public decimal? Hours { get; set; }
    /// <summary>The LDP objective this closes. Matched automatically when omitted.</summary>
    public string? LdpObjectiveId { get; set; }
}

/// <summary>Where one employee stands against the annual training target (HR-026).</summary>
public class TrainingHoursSummaryDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? PositionTitle { get; set; }
    public int Year { get; set; }
    public decimal HoursYtd { get; set; }
    public int TargetHours { get; set; }
    public decimal PercentOfTarget { get; set; }
    public int EventsAttended { get; set; }
    public DateTime? LastTrainingDate { get; set; }
    /// <summary>HR-033 — nothing at all by 30 June.</summary>
    public bool ZeroHoursRedFlag { get; set; }
}

// ── Mandatory training (P23, HR-029/034) ──
public class MandatoryRequirementDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EvidenceSource { get; set; } = string.Empty;
    public int? ValidityMonths { get; set; }
    public bool BlocksIncrement { get; set; }
    public int GraceDays { get; set; }
    public string? DepartmentId { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class SaveMandatoryRequirementDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Hr | Hse | Compliance — which service holds the completion record.</summary>
    public string EvidenceSource { get; set; } = "Hr";
    public int? ValidityMonths { get; set; }
    public bool BlocksIncrement { get; set; } = true;
    public int GraceDays { get; set; } = 30;
    public string? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class MandatoryStatusDto
{
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string EvidenceSource { get; set; } = string.Empty;
    /// <summary>Valid | DueSoon | Overdue | Breached | NeverCompleted | Unknown.</summary>
    public string State { get; set; } = string.Empty;
    public DateTime? CompletedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public int? DaysOverdue { get; set; }
    public bool BlocksIncrement { get; set; }
    /// <summary>Why, in words — including "could not be checked" when the source was unreachable.</summary>
    public string Detail { get; set; } = string.Empty;
}

public class EmployeeComplianceDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }
    public bool HasLoginAccount { get; set; }
    public List<MandatoryStatusDto> Requirements { get; set; } = [];
    public int Breached { get; set; }
    public int Unknown { get; set; }
    public bool AnyBlocksIncrement { get; set; }
}

/// <summary>
/// H7 → H8 — whether this employee may be proposed for a salary increment (HR-029 mandatory training,
/// HR-035 professional certification, plus the L&amp;D hours target).
/// </summary>
public class IncrementEligibilityDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public bool Eligible { get; set; }
    /// <summary>Hard blocks — an increment cannot be proposed while any of these stand.</summary>
    public List<string> Blockers { get; set; } = [];
    /// <summary>Things worth knowing that do not block, including anything that could not be checked.</summary>
    public List<string> Warnings { get; set; } = [];
    public decimal HoursYtd { get; set; }
    public int TargetHours { get; set; }
}

// ── Knowledge sharing (P25) ──
public class KnowledgeSharingSessionDto
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FacilitatorEmployeeId { get; set; } = string.Empty;
    public string? FacilitatorName { get; set; }
    public DateTime SessionDate { get; set; }
    public decimal DurationHours { get; set; }
    public int AttendeeCount { get; set; }
    public string? TrainingEventId { get; set; }
    public string? Notes { get; set; }
    public List<TrainingAttendeeDto> Attendees { get; set; } = [];
}

public class SaveKnowledgeSharingDto
{
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FacilitatorEmployeeId { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public decimal DurationHours { get; set; }
    public List<string> AttendeeEmployeeIds { get; set; } = [];
    public string? Notes { get; set; }
}

// ── L&D budgets (P26) ──
public class LdBudgetDto
{
    public string Id { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public int Year { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal ActualSpend { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentUsed { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public DateTime? Warning80SentAt { get; set; }
    public DateTime? Exhausted100SentAt { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class SaveLdBudgetDto
{
    public string DepartmentId { get; set; } = string.Empty;
    public int? Year { get; set; }
    public decimal BudgetedAmount { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}

// ── Summary and sweep ──
public class LearningSummaryDto
{
    public int Year { get; set; }
    public int PlansExpected { get; set; }
    public int PlansApproved { get; set; }
    public int PlansAwaitingApproval { get; set; }
    public int PlansMissing { get; set; }

    public decimal TotalTrainingHours { get; set; }
    public int EmployeesOnTarget { get; set; }
    public int EmployeesZeroHours { get; set; }
    public int TrainingEvents { get; set; }

    public int MandatoryRequirements { get; set; }
    public int ComplianceBreaches { get; set; }
    public int ComplianceUnknown { get; set; }
    public int BlockedFromIncrement { get; set; }

    public int KnowledgeSessionsThisMonth { get; set; }
    public int KnowledgeSessionsThisYear { get; set; }
    /// <summary>HR-030 wants at least two a month.</summary>
    public bool MonthlySessionTargetMet { get; set; }

    public decimal BudgetTotal { get; set; }
    public decimal BudgetSpent { get; set; }
    public int BudgetsOver80 { get; set; }
    public int BudgetsExhausted { get; set; }
}

public class LearningSweepResultDto
{
    public int LdpRemindersSent { get; set; }
    public int LdpEscalations { get; set; }
    public int ZeroHoursFlags { get; set; }
    public int MandatoryBreachAlerts { get; set; }
    public int KnowledgeSharingShortfalls { get; set; }
    public int BudgetWarnings { get; set; }
    public int BudgetsExhausted { get; set; }
    public List<string> Notes { get; set; } = [];
}
