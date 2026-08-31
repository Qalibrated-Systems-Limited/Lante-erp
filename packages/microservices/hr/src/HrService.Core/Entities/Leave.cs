using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H3 (HR-005, P4 step 4.1) — LEAVE_TYPE. The rules for one kind of leave, all configurable per tenant rather
/// than hard-coded, because the QSL defaults (annual 21, sick 7+7, maternity 90, paternity 14, compassionate 3,
/// study at MD discretion) are policy and policy changes.
/// <para><b>The approval chain is derived from these flags, never from the type's name.</b> Line Manager is
/// always the first step; <see cref="RequiresHrApproval"/> adds HR; <see cref="RequiresBoardApproval"/> adds the
/// Board but only when the request exceeds <see cref="BoardApprovalAfterDays"/> (P5: study leave over 2 weeks).
/// Matching on names would break the moment a tenant renamed "Study" or added a second study-like type.</para>
/// </summary>
public class LeaveType : BaseEntity
{
    /// <summary>Stable machine key (ANNUAL, SICK, MATERNITY, …) — the seeder and the carry-forward job match
    /// on this, so renaming the display <see cref="Name"/> cannot break either.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Full-year entitlement in days. 0 means "no fixed entitlement" (study leave at MD discretion).</summary>
    public decimal DaysAllowed { get; set; }

    /// <summary>Days beyond which the balance is paid at half rate — sick leave is 7 full + 7 half under the
    /// Employment Act. Null means every entitled day is at full pay. Read by H6 payroll, not enforced here.</summary>
    public decimal? FullPayDays { get; set; }

    /// <summary>False for unpaid leave types, so H4 attendance and H6 payroll can deduct (ATT-004).</summary>
    public bool IsPaid { get; set; } = true;

    // ── Carry-forward (P4/P6) ──
    /// <summary>Only annual leave carries forward per the P4 design note; the rest expire with the year.</summary>
    public bool CarriesForward { get; set; }
    /// <summary>Cap on days carried into the new year — QSL policy is 10. Excess is forfeited.</summary>
    public decimal MaxCarryForwardDays { get; set; }

    // ── Documents (P5 step 5.3) ──
    /// <summary>Request length above which a document becomes mandatory (sick leave over 3 days needs a
    /// medical certificate). Null means never; 0 means always (maternity, paternity, study).</summary>
    public int? RequiresDocumentAfterDays { get; set; }
    /// <summary>Which vault document type satisfies the requirement, per HR-DEC-6 (leave documents live in the
    /// H1 employee document vault keyed to the request, not in a separate LEAVE_DOCUMENT table).</summary>
    public EmployeeDocumentType? DocumentTypeRequired { get; set; }

    // ── Approval chain (P5 step 5.4) ──
    public bool RequiresHrApproval { get; set; }
    public bool RequiresBoardApproval { get; set; }
    /// <summary>The Board step only applies above this length — QSL policy is over 14 days for study leave.</summary>
    public int BoardApprovalAfterDays { get; set; }

    // ── Counting ──
    /// <summary>True counts working days only (annual, sick, compassionate, study); false counts calendar days
    /// (maternity's 90 and paternity's 14 are calendar days under the Employment Act).
    /// <para>Weekends and public holidays are both excluded when this is set, via the shared work calendar
    /// attendance also uses (H4) — so leave is never charged for a day nobody was expected to work.</para></summary>
    public bool CountsWorkingDaysOnly { get; set; } = true;

    /// <summary>Pro-rate the first (partial) year by months served — H3-DEC-1. A December joiner gets ~2 days of
    /// annual leave, not 21; from the next January they get the full entitlement.</summary>
    public bool ProRateFirstYear { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

/// <summary>
/// H3 (P4 step 4.2, DS2 LEAVE_ENTITLEMENT) — one employee's allowance of one leave type for one year.
/// <para><b>The balance is derived, never stored.</b> <see cref="DaysEntitled"/> already includes any days
/// carried in, so balance = entitled − taken and there is no second number that can drift out of step with the
/// approved requests. The DFD lists a <c>days_balance</c> column; keeping it would mean every approval,
/// cancellation, carry-forward and expiry had to remember to update it consistently.</para>
/// </summary>
public class LeaveEntitlement : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string LeaveTypeId { get; set; } = string.Empty;
    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }

    public int Year { get; set; }

    /// <summary>The year's allowance including <see cref="CarriedForwardDays"/> (P6 step 6.4:
    /// days_entitled = 21 + carried_forward_days).</summary>
    public decimal DaysEntitled { get; set; }
    /// <summary>Incremented only on FINAL approval of a request (P5 step 5.5).</summary>
    public decimal DaysTaken { get; set; }

    public decimal CarriedForwardDays { get; set; }
    /// <summary>Days lost — either above the carry-forward cap at year end, or carried but unused by 31 March.</summary>
    public decimal ForfeitedDays { get; set; }

    /// <summary>Set when the year's allowance was pro-rated for a mid-year joiner, so the smaller number is
    /// explainable rather than looking like a data error.</summary>
    public bool WasProRated { get; set; }
    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H3 (P5, DS2 LEAVE_REQUEST) — one application and where it is in its chain.
/// <para>The requested days are computed server-side from the dates and the leave type's counting rule; the
/// client does not get to say how many days its own request costs.</para>
/// </summary>
public class LeaveRequest : BaseEntity
{
    public string RequestNumber { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    public string LeaveTypeId { get; set; } = string.Empty;
    public string? LeaveTypeCode { get; set; }
    public string? LeaveTypeName { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>The day the employee is back at work (P5 step 5.1) — normally the next working day after
    /// <see cref="EndDate"/>, but recorded rather than inferred because travel days vary.</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>Computed from the dates and the type's working-day rule. Never taken from the client.</summary>
    public decimal DaysRequested { get; set; }

    public string? Reason { get; set; }
    public string? HandoverNotes { get; set; }
    /// <summary>Who covers the work while they are away (P5 step 5.1).</summary>
    public string? CoverEmployeeId { get; set; }
    public string? CoverEmployeeName { get; set; }

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    /// <summary>1-based position in the chain that is currently waiting; 0 once the request is closed.</summary>
    public int CurrentStep { get; set; } = 1;
    public int TotalSteps { get; set; } = 1;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? SubmittedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Whether this request's length tripped the type's document rule. Held on the request because the
    /// type's threshold can change later and the requirement at the time of applying is what was enforced.</summary>
    public bool DocumentRequired { get; set; }
    public EmployeeDocumentType? RequiredDocumentType { get; set; }

    public Employee? Employee { get; set; }
    public List<LeaveApprovalLog> ApprovalLog { get; set; } = [];
}

/// <summary>
/// H3 (P5 step 5.4, DS5 LEAVE_APPROVAL_LOG) — one step of one request's chain.
/// <para>Every step is written as a <see cref="LeaveApprovalAction.Pending"/> row when the request is submitted
/// rather than appended as approvals happen. The chain is then visible up front ("this needs LM, then HR, then
/// the Board"), "who is it waiting on" is a query rather than a calculation, and a rejection can close the
/// remaining steps as <see cref="LeaveApprovalAction.Skipped"/> instead of leaving them missing.</para>
/// </summary>
public class LeaveApprovalLog : BaseEntity
{
    public string LeaveRequestId { get; set; } = string.Empty;
    public int Step { get; set; }
    public LeaveApprovalRole Role { get; set; }

    public LeaveApprovalAction Action { get; set; } = LeaveApprovalAction.Pending;
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime? ActionedAt { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }
}

/// <summary>
/// H3 (P4 step 4.4 / P6, DS3 LEAVE_CARRY_FORWARD) — one year-end carry decision for one employee.
/// <para>A zero-day row is still written when there was nothing to carry (P6 step 6.2c, "for completeness"),
/// which also makes the row the idempotence key: the 31 December job asks "does a row exist for this employee,
/// type and from-year" rather than remembering whether it has run.</para>
/// </summary>
public class LeaveCarryForward : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string LeaveTypeId { get; set; } = string.Empty;
    public string? LeaveTypeCode { get; set; }

    public int FromYear { get; set; }
    public int ToYear { get; set; }

    public decimal DaysCarried { get; set; }
    /// <summary>Days above the cap, lost at year end.</summary>
    public decimal DaysForfeited { get; set; }
    /// <summary>Of <see cref="DaysCarried"/>, how many were lost unused at the 31 March expiry.</summary>
    public decimal DaysExpired { get; set; }

    /// <summary>31 March of <see cref="ToYear"/> — carried days must be used in Q1.</summary>
    public DateTime ExpiryDate { get; set; }
    public CarryForwardStatus Status { get; set; } = CarryForwardStatus.Active;
    public DateTime? ExpiredAt { get; set; }
    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
}
