using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H11 (P29 step 29.1, DS1 COMMISSION_BAND) — one attainment band and the rate it pays.
/// <para>Bands are dated like the statutory rates are: a statement re-computed for an old quarter must use
/// the scale that applied then, so a rate change adds rows rather than editing these.</para>
/// </summary>
public class CommissionBand : BaseEntity
{
    public string Label { get; set; } = string.Empty;
    /// <summary>Attainment percentage this band starts at — exclusive, so bands cannot overlap.</summary>
    public decimal MinPercent { get; set; }
    /// <summary>Inclusive. Null means the top band — no ceiling.</summary>
    public decimal? MaxPercent { get; set; }
    /// <summary>Commission as a percentage of revenue collected.</summary>
    public decimal CommissionRatePercent { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// H11 (P29 step 29.2, DS2 COMMISSION_TARGET) — the MD-approved commission BASIS for one Sales Engineer for
/// one year.
/// <para><b>H11-DEC-1: this deliberately holds NO revenue target.</b> The target lives in
/// <c>crm.SalesTarget</c> and is read; storing a second copy here is exactly how two numbers for "the annual
/// target" come to disagree. What the MD approves is that this person is <i>on commission</i> for the year and
/// on what basis — the target itself is the sales function's number, not HR's.</para>
/// </summary>
public class CommissionPlan : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public int Year { get; set; }

    public CommissionPlanStatus Status { get; set; } = CommissionPlanStatus.Draft;
    public string? Basis { get; set; }
    public string? Notes { get; set; }

    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H11 (P30, COM-004) — one quarter's commission for one Sales Engineer.
/// <para><b>The target and attainment are SNAPSHOTTED here, and that is not a contradiction of H11-DEC-1.</b>
/// HR does not own the target; it records what it read at the moment it computed. A statement issued in April
/// must still explain itself in December, after CRM's live numbers have moved on.</para>
/// <para>Payment runs through payroll on the same claim-and-release machinery as overtime, so commission is
/// taxed correctly (COM-005) and cannot be paid twice.</para>
/// </summary>
public class CommissionStatement : BaseEntity
{
    /// <summary>CS-{year}-Q{n}-{seq}.</summary>
    public string StatementNumber { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? CommissionPlanId { get; set; }

    public int Year { get; set; }
    /// <summary>1–4. COM-004 issues these on 1 Apr, 1 Jul, 1 Oct and 1 Jan.</summary>
    public int Quarter { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // ── What was read from CRM at computation time ──
    public decimal AnnualTarget { get; set; }
    /// <summary>The annual target divided by four (P29 step 29.4).</summary>
    public decimal QuarterTarget { get; set; }
    public decimal RevenueAchieved { get; set; }
    /// <summary>Year-to-date revenue against the FULL annual target (DFD step 30.2). This is what selects the
    /// commission band, so the rate builds as the year's cumulative attainment builds.</summary>
    public decimal AttainmentPercent { get; set; }
    /// <summary>
    /// Year-to-date revenue against the target PRO-RATED to the quarters elapsed — annual x N/4 (step 29.4).
    /// <para><b>This, not <see cref="AttainmentPercent"/>, is what the COM-007/COM-008 red flags test.</b> The
    /// two denominators are not interchangeable: someone exactly on plan at the end of Q1 has banked a quarter
    /// of the annual target, so measuring them against the annual figure reads 25% and escalates a CRITICAL
    /// under-performance alert to the MD about somebody who is performing precisely to plan.</para>
    /// </summary>
    public decimal ProRatedAttainmentPercent { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    /// <summary>Where the figures came from, including "could not be read".</summary>
    public string? SourceNotes { get; set; }

    // ── The band that applied and what it paid ──
    public string? CommissionBandId { get; set; }
    public string? BandLabel { get; set; }
    public decimal CommissionRatePercent { get; set; }

    // CRM reports revenue cumulatively for the year, and the band is set by attainment against the ANNUAL
    // target — so commission has to be settled year-to-date and netted against what earlier quarters already
    // paid. Paying (cumulative revenue x rate) every quarter would pay Q1's revenue again in Q2, Q3 and Q4.
    // Settling this way also tops earlier quarters up correctly when a later quarter reaches a higher band.
    /// <summary>Commission the year-to-date revenue has earned in total, at this quarter's rate.</summary>
    public decimal CommissionEarnedToDate { get; set; }
    /// <summary>What the year's earlier quarters already approved or paid.</summary>
    public decimal PriorCommissionThisYear { get; set; }
    /// <summary>This quarter's settlement: earned-to-date less what has already been settled.</summary>
    public decimal CommissionAmount { get; set; }
    /// <summary>Set when earned-to-date fell below what was already paid — see <see cref="CommissionAmount"/>,
    /// which is floored at zero. Recovering an overpayment from pay is a policy and legal decision, not one
    /// this service makes silently.</summary>
    public decimal? UnrecoveredOverpayment { get; set; }

    public CommissionStatementStatus Status { get; set; } = CommissionStatementStatus.Computed;
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    public string? ComputedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CancellationReason { get; set; }

    // ── Payroll hand-off (COM-005), mirroring OvertimeRequest ──
    public string? PayrollPeriodId { get; set; }
    public string? PayrollPeriodCode { get; set; }
    /// <summary>Stamped when a run pays it — the guard against paying the same commission twice.</summary>
    public string? PayrollRunId { get; set; }
    public decimal? PaidAmount { get; set; }

    /// <summary>COM-007/008 — the quarter-end red flag this statement raised, if any.</summary>
    public string? RedFlag { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H11 (P31, COM-006) — a contested statement. Raising one freezes the payment: paying a figure somebody is
/// formally contesting is how a dispute turns into a grievance.
/// </summary>
public class CommissionDispute : BaseEntity
{
    public string CommissionStatementId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal? DisputedAmount { get; set; }

    public CommissionDisputeStatus Status { get; set; } = CommissionDisputeStatus.Open;
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    public string? RaisedBy { get; set; }

    public string? AssignedTo { get; set; }
    public string? Findings { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    /// <summary>Where the dispute was upheld and the figure changed — kept so the movement is explicable.</summary>
    public decimal? AdjustedAmount { get; set; }
}
