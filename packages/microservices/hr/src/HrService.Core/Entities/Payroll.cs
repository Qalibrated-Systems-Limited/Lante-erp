using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H5 (P7, DS1 JOB_GRADE) — a pay band. Kept separate from <see cref="Position"/> on purpose (H5-DEC-3): a job
/// title says what someone does, a grade says what the band pays, and two different titles can sit in one grade.
/// </summary>
public class JobGrade : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Advisory band edges — a salary assignment outside them is warned about, not blocked, because
    /// out-of-band pay is a real (if deliberate) decision that HR and the MD make with their eyes open.</summary>
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// H5 (P7 step 7.1, DS2 SALARY_STRUCTURE) — a named set of components for one grade.
/// <para>Structures are versioned by use, not by edit: components can be added and deactivated, but an
/// employee's own numbers live on <see cref="EmployeeSalary"/>, so changing a structure never silently rewrites
/// what somebody was already assigned.</para>
/// </summary>
public class SalaryStructure : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? JobGradeId { get; set; }
    public string? JobGradeCode { get; set; }

    /// <summary>ISO code — KES unless a tenant pays in something else. Finance owns the currency master; HR
    /// carries the code so a payslip can state it without a cross-service call on every read.</summary>
    public string CurrencyCode { get; set; } = "KES";

    public bool IsActive { get; set; } = true;

    public JobGrade? JobGrade { get; set; }
    public List<SalaryComponent> Components { get; set; } = [];
}

/// <summary>
/// H5 (P7 steps 7.2–7.3 + 7.5, DS3 SALARY_COMPONENT) — one line of a structure, and where it posts.
/// <para>Every component carries its GL account because the payroll journal is assembled from these
/// mappings (P7 step 7.5): without an account on the line, H6 would have nowhere to post it. The account id
/// belongs to finance's chart of accounts; the code and name are denormalised so a payslip or a mapping screen
/// reads correctly even when finance is unreachable.</para>
/// </summary>
public class SalaryComponent : BaseEntity
{
    public string SalaryStructureId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    /// <summary>Stable key (BASIC, HOUSE, TRANSPORT, PAYE, …) — the H6 engine matches on this.</summary>
    public string Code { get; set; } = string.Empty;

    public SalaryComponentType ComponentType { get; set; }
    public ComponentCalculationType CalculationType { get; set; } = ComponentCalculationType.FixedAmount;

    /// <summary>Used by <see cref="ComponentCalculationType.FixedAmount"/>.</summary>
    public decimal? Amount { get; set; }
    /// <summary>Used by the percent-of-basic and percent-of-gross calculations.</summary>
    public decimal? Percentage { get; set; }

    /// <summary>Which statutory rule computes this line, when <see cref="CalculationType"/> is Statutory.</summary>
    public StatutoryComponent Statutory { get; set; } = StatutoryComponent.None;

    /// <summary>Taxable earnings form the PAYE base; non-taxable ones (some reimbursements) do not.</summary>
    public bool IsTaxable { get; set; } = true;

    // ── GL mapping (P7 step 7.5) ──
    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }

    /// <summary>Payslip ordering (P7 design note).</summary>
    public int ComponentOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public SalaryStructure? SalaryStructure { get; set; }
}

/// <summary>
/// H5 — a monthly payroll period. Both P7 and P8 key their effective dates on a period id, and H6's payroll run
/// needs one, so HR owns its own monthly calendar rather than borrowing finance's fiscal periods (HR-DEC-4: HR
/// computes, finance posts).
/// </summary>
public class PayrollPeriod : BaseEntity
{
    /// <summary>"2026-07" — sortable and human-readable, and the natural key.</summary>
    public string Code { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>HR-008 — the cut-off after which changes belong to the next period (the 20th at QSL).</summary>
    public DateTime? CutOffDate { get; set; }
    public DateTime? PaymentDate { get; set; }

    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Open;
    public DateTime? LockedAt { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// H5 (P7 step 7.4, DS6 EMPLOYEE_SALARY) — what one employee is paid, from one period onwards.
/// <para><b>Never edited.</b> A raise writes a NEW record for the new period and marks the old one
/// <see cref="SalaryAssignmentStatus.Superseded"/> (P7 design note), so the answer to "what were they on in
/// March" survives every later change. HR proposes and the MD approves; an unapproved assignment does not pay.</para>
/// </summary>
public class EmployeeSalary : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string SalaryStructureId { get; set; } = string.Empty;
    public string? SalaryStructureName { get; set; }

    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    /// <summary>The period this assignment starts paying from.</summary>
    public string EffectiveFromPeriodId { get; set; } = string.Empty;
    public string? EffectiveFromPeriodCode { get; set; }

    public SalaryAssignmentStatus Status { get; set; } = SalaryAssignmentStatus.Proposed;

    public string? ProposedBy { get; set; }
    public DateTime? ProposedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>The assignment this one replaced, so the pay history is a chain rather than a pile.</summary>
    public string? SupersededById { get; set; }
    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
    public SalaryStructure? SalaryStructure { get; set; }
}

/// <summary>
/// H5 (P7 step 7.3, DS4 PAYE_TAX_BAND) — one band of the progressive income-tax scale.
/// <para>Bands are dated rather than replaced: a payroll re-run for an earlier month must use the rates that
/// applied then, so a rate change adds rows with a new <see cref="EffectiveFrom"/> instead of editing these.</para>
/// <para><see cref="NeedsConfirmation"/> is set on everything the seeder installs. The rates were correct when
/// written but tax law moves every Finance Act, and a seeded number that nobody checked must not be mistaken for
/// a verified one — H6 surfaces the flag before it will run real payroll.</para>
/// <para><b>Bounds are half-open: a band taxes the slice above <see cref="LowerBound"/> up to and including
/// <see cref="UpperBound"/>.</b> The first band therefore starts at 0 and each subsequent band's lower bound
/// equals the previous band's upper bound. Stated as inclusive-both-ends ("24,001–32,333") the shillings
/// between 24,000.00 and 24,001.00 would fall through untaxed; the half-open reading has no such gap and is
/// what the contiguity check in PayrollService enforces.</para>
/// </summary>
public class PayeTaxBand : BaseEntity
{
    /// <summary>Exclusive — income at exactly this figure belongs to the band below.</summary>
    public decimal LowerBound { get; set; }
    /// <summary>Inclusive. Null means the top band — no ceiling.</summary>
    public decimal? UpperBound { get; set; }
    /// <summary>Percentage, e.g. 30 for 30%.</summary>
    public decimal Rate { get; set; }

    public int BandOrder { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Seeded from record and never verified against the current Finance Act.</summary>
    public bool NeedsConfirmation { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// H5 (P7 step 7.3, DS5 TAX_CATEGORY) — the non-PAYE statutory rates: NSSF's tiers, SHA, the Housing Levy,
/// HELB and personal relief.
/// <para>One row per rule per tier per effective date, dated for the same reason the PAYE bands are, and carrying
/// its own GL account so the H6 journal knows where each statutory liability posts.</para>
/// </summary>
public class StatutoryRate : BaseEntity
{
    /// <summary>NSSF_TIER1, NSSF_TIER2, SHA, HOUSING_LEVY, HELB, PERSONAL_RELIEF.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public StatutoryComponent Component { get; set; }
    public StatutoryRateType RateType { get; set; }

    /// <summary>Percentage for the percent and tiered types; ignored by the others.</summary>
    public decimal? Rate { get; set; }
    /// <summary>Flat figure for <see cref="StatutoryRateType.FixedAmount"/> (personal relief).</summary>
    public decimal? FixedAmount { get; set; }

    // Tier edges for NSSF.
    public decimal? TierLowerBound { get; set; }
    public decimal? TierUpperBound { get; set; }

    /// <summary>Floor and ceiling on the computed figure — SHA has a minimum, NSSF tiers have caps.</summary>
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    /// <summary>The employer's matching percentage where there is one (NSSF, Housing Levy). Employer cost, not
    /// an employee deduction — H6 posts it separately.</summary>
    public decimal? EmployerRate { get; set; }

    /// <summary>
    /// H6 — whether this contribution comes off pay BEFORE PAYE is computed.
    /// <para>This is data rather than code on purpose. Which statutory contributions are allowable against
    /// taxable pay is exactly the sort of thing that moves with each Finance Act — NSSF has long been
    /// deductible, and SHA and the Housing Levy have been treated differently at different times. Hard-coding
    /// today's answer would bake a date-stamped legal opinion into the engine; holding it on the dated rate row
    /// means a re-run of an old month uses the rule that applied then, and a change needs no code.</para>
    /// <para>Seeded values carry <see cref="NeedsConfirmation"/> like every other seeded figure.</para>
    /// </summary>
    public bool ReducesTaxableIncome { get; set; }

    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool NeedsConfirmation { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// H5 (P8 step 8.1, DS1 PAYROLL_DEDUCTION_TYPE) — the reusable deduction catalogue: configure once, apply to
/// many (P8 design note).
/// </summary>
public class PayrollDeductionType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DeductionCategory Category { get; set; }

    /// <summary>Whether the deduction comes off pay before tax is computed (a pension contribution does; a
    /// court order does not). Named to match <see cref="StatutoryRate.ReducesTaxableIncome"/> — the two
    /// flags feed the same pre-tax subtraction in <c>PayrollRunService.ComputeForEmployee</c> and are the
    /// same concept, not a coincidence.</summary>
    public bool ReducesTaxableIncome { get; set; }
    /// <summary>Recurring deductions run every period until stopped; one-off ones are taken once (P8 step 8.3).</summary>
    public bool IsRecurring { get; set; } = true;

    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

/// <summary>
/// H5 (P8 steps 8.2–8.4, DS3 PAYROLL_DEDUCTION) — one deduction applied to one employee.
/// <para><b>Stopping never deletes</b> (P8 design note): <see cref="IsActive"/> goes false with
/// <see cref="RemovedBy"/> and <see cref="RemovedAt"/> stamped and the end period capped, so the deduction trail
/// stays auditable. A one-off deduction ends in the period it starts.</para>
/// </summary>
public class PayrollDeduction : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string DeductionTypeId { get; set; } = string.Empty;
    public string? DeductionTypeCode { get; set; }
    public string? DeductionTypeName { get; set; }
    public DeductionCategory Category { get; set; }

    /// <summary>The amount per period.</summary>
    public decimal Amount { get; set; }

    public string StartPeriodId { get; set; } = string.Empty;
    public string? StartPeriodCode { get; set; }
    /// <summary>Null means "until stopped". Equal to the start period for a one-off.</summary>
    public string? EndPeriodId { get; set; }
    public string? EndPeriodCode { get; set; }

    public bool IsActive { get; set; } = true;

    public string? AddedBy { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string? RemovedBy { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
    public PayrollDeductionType? DeductionType { get; set; }
}
