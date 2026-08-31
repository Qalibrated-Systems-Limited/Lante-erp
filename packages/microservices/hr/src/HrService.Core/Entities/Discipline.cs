using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H10 (P18, HR-021) — a disciplinary case, from incident to appeal.
/// <para><b>Every stage stamps its own timestamp and cannot be taken out of turn.</b> The sequence is not
/// bureaucracy: a hearing held before the employee was ever asked to explain themselves is not a hearing, and
/// an outcome recorded without one is not a decision. The audit trail here is legally load-bearing (P18 DS4).</para>
/// <para><see cref="SourceModule"/> lets a case be raised FROM something else — an operations negligence
/// incident, a failed improvement plan — without HR copying that record. Operations keeps owning its
/// negligence log; HR owns the formal process that may follow from it.</para>
/// </summary>
public class DisciplinaryCase : BaseEntity
{
    /// <summary>DC-{year}-{seq}.</summary>
    public string CaseNumber { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    public DateTime IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Witnesses { get; set; }

    /// <summary>Where this came from: "Operations" for a negligence incident, "HR" for a failed improvement
    /// plan, null when HR raised it directly. HR never copies the source record — it points at it.</summary>
    public string? SourceModule { get; set; }
    public string? SourceReference { get; set; }

    public DisciplinaryStatus Status { get; set; } = DisciplinaryStatus.Open;

    // ── Show cause (P18 step 18.2) — five WORKING days, via the shared calendar ──
    public DateTime? ShowCauseIssuedAt { get; set; }
    public string? ShowCauseLetter { get; set; }
    public DateTime? ResponseDeadline { get; set; }
    public DateTime? EmployeeRespondedAt { get; set; }
    public string? EmployeeResponse { get; set; }
    /// <summary>Stamped once when the response window lapses, so the sweep chases it only once.</summary>
    public DateTime? ResponseOverdueAlertedAt { get; set; }

    // ── Hearing and outcome (P18 steps 18.3–18.4) ──
    public DateTime? HearingDate { get; set; }
    public string? HearingPanel { get; set; }
    public string? HearingNotes { get; set; }
    public DisciplinaryOutcome Outcome { get; set; } = DisciplinaryOutcome.None;
    public string? OutcomeNotes { get; set; }
    public DateTime? OutcomeRecordedAt { get; set; }
    public string? OutcomeRecordedBy { get; set; }

    // ── Appeal (P18 step 18.5) — fourteen days ──
    public DateTime? RightOfAppealDeadline { get; set; }
    public DateTime? AppealSubmittedAt { get; set; }
    public string? AppealGrounds { get; set; }
    public string? AppealOutcome { get; set; }
    public DateTime? AppealDecidedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    /// <summary>The warning a Warning outcome produced, when it did.</summary>
    public string? WarningRecordId { get; set; }
    /// <summary>The separation a Termination outcome opened (P18 step 18.4b → P21).</summary>
    public string? SeparationId { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H10 (P19, HR-022, DS3 WARNING_RECORD) — one warning and how long it stands.
/// <para>Warnings expire rather than being deleted: three in a rolling twelve months triggers a termination
/// review, and that count is only meaningful if expired warnings are still on file with their dates.</para>
/// </summary>
public class WarningRecord : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string? DisciplinaryCaseId { get; set; }
    public WarningType WarningType { get; set; }

    public DateTime IncidentDate { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Reason { get; set; }

    /// <summary>False once the expiry date passes. The row stays — see the class remarks.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiredAt { get; set; }

    public bool AcknowledgedByEmployee { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgementNote { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H10 (P20, HR-023) — a grievance raised by an employee.
/// <para>HR must acknowledge within two WORKING days; missing that is an SLA breach that goes to the MD
/// (P20 step 20.2). The deadline is computed on the shared work calendar, so a grievance raised on a Friday
/// is not late because of the weekend.</para>
/// </summary>
public class GrievanceCase : BaseEntity
{
    /// <summary>GR-{year}-{seq} — the reference the employee is given.</summary>
    public string CaseNumber { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Raised in confidence — the detail is restricted to HR and the assigned investigator.</summary>
    public bool IsConfidential { get; set; }

    public GrievanceStatus Status { get; set; } = GrievanceStatus.Submitted;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AcknowledgementDeadline { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    /// <summary>One-way stamp so the SLA breach is escalated once, not every morning.</summary>
    public DateTime? SlaBreachAlertedAt { get; set; }

    public string? AssignedToEmployeeId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? AssignedAt { get; set; }

    public string? InvestigationFindings { get; set; }
    public string? Outcome { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    /// <summary>Follow-up actions, where the outcome was only partial.</summary>
    public string? FollowUpActions { get; set; }
    public DateTime? EscalatedAt { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H10 (P21, HR-024, DS4 SEPARATION) — an employee leaving, and what they are owed.
/// <para><b>Every component of the final dues is stored, not just the total.</b> A leaver who disputes their
/// payment needs to see the arithmetic, and "total dues 143,000" is not an answer to "why".</para>
/// <para><b>The employee is deactivated on PAYMENT, not on approval.</b> Somebody is on the payroll until they
/// have actually been paid what they are owed — deactivating at approval would drop them from a payroll run
/// that still owed them money.</para>
/// </summary>
public class Separation : BaseEntity
{
    /// <summary>SEP-{year}-{seq}.</summary>
    public string SeparationNumber { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }
    public DateTime HireDate { get; set; }

    public SeparationType SeparationType { get; set; }
    public DateTime NoticeDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public int NoticePeriodDays { get; set; }
    /// <summary>Notice waived — the employee leaves at once and is paid in lieu (P21 summary).</summary>
    public bool NoticeWaived { get; set; }
    public string? Reason { get; set; }

    /// <summary>Where this came from — a disciplinary termination, a probation decision, a lapsed contract.</summary>
    public string? SourceModule { get; set; }
    public string? SourceReference { get; set; }

    // ── Final dues (P21 step 21.2). Each component stored so the total can be explained. ──
    public decimal MonthlySalary { get; set; }
    public decimal DailyRate { get; set; }
    public decimal LeaveDaysBalance { get; set; }
    public decimal LeavePayout { get; set; }
    public int FinalMonthDaysWorked { get; set; }
    public decimal ProRataSalary { get; set; }
    public decimal NoticePay { get; set; }
    public decimal OtherEarnings { get; set; }
    /// <summary>Outstanding staff advances recovered from the payment. Read from finance where possible,
    /// entered by HR where not — never assumed to be zero.</summary>
    public decimal AdvanceRecovery { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal NetDues { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    /// <summary>How the advance figure was arrived at, including "could not be checked".</summary>
    public string? DuesNotes { get; set; }

    public SeparationStatus Status { get; set; } = SeparationStatus.Draft;
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? PaidBy { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentReference { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>P21 step 21.6 — the Certificate of Service, once issued.</summary>
    public DateTime? CertificateIssuedAt { get; set; }
    public string? ExitInterviewNotes { get; set; }

    public Employee? Employee { get; set; }
}
