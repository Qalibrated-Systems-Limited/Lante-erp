namespace HrService.Core.Enums;

/// <summary>H5 (P7 step 7.2) — whether a salary component adds to pay or takes away from it.</summary>
public enum SalaryComponentType
{
    Earning,
    Deduction,
}

/// <summary>
/// H5 (P7 step 7.2, <c>calculation_type</c>) — how a component's amount is arrived at.
/// <para><see cref="Statutory"/> components carry no amount of their own: they are computed by the H6 engine
/// from the rate tables (<see cref="PayeTaxBand"/> / <c>StatutoryRate</c>), which is why the component only
/// declares WHICH statutory rule applies and where it posts.</para>
/// </summary>
public enum ComponentCalculationType
{
    /// <summary>A flat figure, the same every month.</summary>
    FixedAmount,
    /// <summary>A percentage of the employee's basic salary.</summary>
    PercentOfBasic,
    /// <summary>A percentage of gross pay (basic plus all taxable earnings).</summary>
    PercentOfGross,
    /// <summary>Computed from a statutory rate table — see <see cref="StatutoryComponent"/>.</summary>
    Statutory,
    /// <summary>Varies per employee per month and is supplied at payroll time (overtime, commission).</summary>
    VariableInput,
}

/// <summary>
/// H5 — the statutory rules a component can be bound to (H5-DEC-1: all five apply).
/// <para>PAYE, NSSF, SHA and Housing Levy are rate-driven and apply to everyone. HELB is statutory but its
/// amount is per-employee — the rate row declares that it exists and where it posts, while the figure comes
/// from that employee's HELB deduction record, because only staff with a student loan owe it.</para>
/// </summary>
public enum StatutoryComponent
{
    None,
    /// <summary>Progressive income tax from <see cref="PayeTaxBand"/>, less personal relief.</summary>
    Paye,
    /// <summary>NSSF Act 2013 tiered pension contribution.</summary>
    Nssf,
    /// <summary>Social Health Authority contribution.</summary>
    Sha,
    /// <summary>Affordable Housing Levy — universal.</summary>
    HousingLevy,
    /// <summary>Higher Education Loans Board repayment — only for staff who owe it.</summary>
    Helb,
}

/// <summary>H5 — how a statutory rate is applied.</summary>
public enum StatutoryRateType
{
    /// <summary>A percentage of gross pay (SHA at 2.75%, Housing Levy at 1.5%).</summary>
    PercentOfGross,
    /// <summary>A percentage of earnings within a band — NSSF's tiers.</summary>
    TieredPercent,
    /// <summary>A flat monthly figure (personal relief).</summary>
    FixedAmount,
    /// <summary>Declared centrally but the amount is held per employee (HELB).</summary>
    PerEmployeeAmount,
}

/// <summary>
/// H5 (P7 step 7.4) — an employee's salary assignment needs the MD, so it has its own small workflow.
/// <para>A salary change never edits an existing record (P7 design note): the old one becomes
/// <see cref="Superseded"/> and a new record is written for the new period, so pay history stays intact.</para>
/// </summary>
public enum SalaryAssignmentStatus
{
    Proposed,
    Approved,
    Rejected,
    /// <summary>Replaced by a later assignment from a later period.</summary>
    Superseded,
}

/// <summary>H5 (P8 step 8.1) — the deduction catalogue's categories, as the DFD enumerates them.</summary>
public enum DeductionCategory
{
    Statutory,
    Loan,
    Voluntary,
    CourtOrder,
    Advance,
}

/// <summary>
/// H5 — a payroll period's life. Payroll periods are HR's own (monthly), distinct from finance's fiscal
/// periods: HR computes pay, finance posts it (HR-DEC-4).
/// </summary>
public enum PayrollPeriodStatus
{
    /// <summary>Accepting salary assignments and deductions.</summary>
    Open,
    /// <summary>A payroll run is in progress — configuration is frozen so the run cannot shift under itself.</summary>
    Locked,
    /// <summary>Paid and posted; nothing further changes.</summary>
    Closed,
}

/// <summary>H6 (P12) — the Employment Act 2007 overtime multipliers, chosen from the DATE rather than
/// declared by the requester: whether a day is a rest day or a gazetted holiday is a fact.</summary>
public enum OvertimeRateType
{
    /// <summary>Ordinary working day — 1.5×.</summary>
    Weekday,
    /// <summary>Rest day or gazetted public holiday — 2×.</summary>
    RestDayOrHoliday,
}

/// <summary>H6 (P12) — overtime is pre-approved; a rejected or pending request is never paid.</summary>
public enum OvertimeStatus
{
    Pending,
    Approved,
    Rejected,
    /// <summary>Paid by a payroll run and no longer amendable.</summary>
    Paid,
}

/// <summary>
/// H6 (P9) — a payroll run's life. Only <see cref="Approved"/> reaches finance, and only a second officer
/// can get it there.
/// </summary>
public enum PayrollRunStatus
{
    /// <summary>Header created, nothing computed yet.</summary>
    Draft,
    /// <summary>Payslips built and totalled, awaiting approval. Safe to recompute.</summary>
    Computed,
    /// <summary>Signed off; the journal is posted and the figures are frozen.</summary>
    Approved,
    /// <summary>Abandoned — every overtime and absence claim it held has been released.</summary>
    Cancelled,
}

/// <summary>H6 — what a payslip line represents. Employer costs appear on the journal and the company's
/// cost of employment, never in the employee's own deductions.</summary>
public enum PayslipLineType
{
    Earning,
    Deduction,
    /// <summary>Employer-side NSSF and housing levy — a company cost, shown for transparency.</summary>
    EmployerCost,
}

/// <summary>
/// H6 (P11, HR-011) — the bank bulk-salary formats QSL uses.
/// <para>Each layout is an assumption until someone checks it against the bank's own template — see
/// <c>BankPaymentFile.NeedsFormatConfirmation</c>.</para>
/// </summary>
public enum BankFormat
{
    /// <summary>Neutral four-column CSV — no bank-specific claim made.</summary>
    Generic,
    Kcb,
    Equity,
    Ncba,
    CoOp,
}

/// <summary>
/// H8 (P13) — a salary increment's life. A proposal only exists once the eligibility gates have passed, so
/// there is no "blocked" state: a blocked proposal is refused outright and never written.
/// </summary>
public enum SalaryIncrementStatus
{
    /// <summary>Raised by HR and awaiting the MD.</summary>
    PendingMd,
    Approved,
    Rejected,
    /// <summary>Pulled by HR before the MD decided.</summary>
    Withdrawn,
}
