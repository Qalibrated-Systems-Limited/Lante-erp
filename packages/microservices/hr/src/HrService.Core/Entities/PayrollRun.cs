using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H6 (P12, DS2 OVERTIME_REQUEST) — overtime, approved BEFORE it is worked (ATT-007).
/// <para>Retrospective claims are deliberately not supported: the whole point of pre-approval is that the
/// manager decides whether the cost is worth incurring while it can still be avoided.</para>
/// <para><see cref="PayrollRunId"/> is stamped when a run picks the hours up, which is what stops the same
/// overtime being paid twice — a re-run releases the stamp before it recomputes.</para>
/// </summary>
public class OvertimeRequest : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public DateTime Date { get; set; }
    public decimal Hours { get; set; }

    /// <summary>Derived from the date against the shared work calendar, never taken from the caller —
    /// whether a day is a Sunday or a gazetted holiday is a fact, not an opinion (P12 step 12.2).</summary>
    public OvertimeRateType RateType { get; set; }
    /// <summary>1.5 or 2.0, resolved from <see cref="RateType"/> and stored so a historical request keeps the
    /// multiplier that applied when it was raised.</summary>
    public decimal Multiplier { get; set; }

    public string? Reason { get; set; }
    public OvertimeStatus Status { get; set; } = OvertimeStatus.Pending;

    public string? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }

    /// <summary>Set when a payroll run pays these hours; null means still unpaid.</summary>
    public string? PayrollRunId { get; set; }
    /// <summary>What the run actually paid for these hours, kept so a payslip can be explained later.</summary>
    public decimal? PaidAmount { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H6 (P9, DS1 PAYROLL_RUN) — one month's payroll for the whole company.
/// <para>A run is computed, reviewed, then approved by a second officer; only approval posts anything to
/// finance. Recomputing a run that has not been approved is safe and expected — it releases every overtime and
/// unpaid-absence stamp it had taken before rebuilding, so the arithmetic never drifts from its inputs.</para>
/// <para><b>One live run per period</b> (P9 step 9.1): the duplicate check is what stops a month being paid
/// twice. A cancelled run releases its claims and lets a fresh one be started.</para>
/// </summary>
public class PayrollRun : BaseEntity
{
    /// <summary>PR-{period}, e.g. PR-2026-08 — one live run per period makes this the natural key.</summary>
    public string RunNumber { get; set; } = string.Empty;

    public string PayrollPeriodId { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? CutOffDate { get; set; }

    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public string CurrencyCode { get; set; } = "KES";

    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalTaxable { get; set; }
    public decimal TotalPaye { get; set; }
    public decimal TotalStatutory { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    /// <summary>Employer-side NSSF and levy — a company cost, not an employee deduction.</summary>
    public decimal TotalEmployerCost { get; set; }

    public string? ComputedBy { get; set; }
    public DateTime? ComputedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>The finance journal raised on approval. Null means nothing has reached the ledger yet.</summary>
    public string? JournalEntryId { get; set; }
    public string? JournalEntryNo { get; set; }
    public DateTime? JournalPostedAt { get; set; }
    /// <summary>Why the journal did not post, when the finance hop failed. Approval still stands — the run is
    /// the record, and posting is retryable.</summary>
    public string? JournalError { get; set; }

    /// <summary>Employees skipped and why (no approved salary, no structure), so a short run is explicable.</summary>
    public string? Exclusions { get; set; }
    public string? Notes { get; set; }

    public List<Payslip> Payslips { get; set; } = [];
}

/// <summary>
/// H6 (P9 step 9.7 / P10, DS8 PAYSLIP) — one employee's pay for one run.
/// <para>Every figure here is a stored result, not a formula evaluated on read: a payslip must say the same
/// thing in five years' time even after the tax bands, the salary structure and the employee's own salary have
/// all moved on.</para>
/// </summary>
public class Payslip : BaseEntity
{
    public string PayrollRunId { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>Statutory ids as they stood at the run, for the payslip and the P9 certificate.</summary>
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }

    public string? EmployeeSalaryId { get; set; }
    public string? SalaryStructureName { get; set; }
    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    public decimal GrossPay { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal Paye { get; set; }
    /// <summary>Relief actually applied — capped at the tax due, since relief never becomes a refund.</summary>
    public decimal PersonalRelief { get; set; }
    public decimal StatutoryDeductions { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal EmployerCost { get; set; }

    public decimal OvertimeHours { get; set; }
    public decimal OvertimePay { get; set; }
    /// <summary>Unpaid days taken off this payslip, from H4's absence records.</summary>
    public decimal UnpaidDays { get; set; }
    public decimal UnpaidDeduction { get; set; }

    // ── Distribution (P10). Left null in this pass rather than stamped for things that never happened. ──
    public string? PdfUrl { get; set; }
    public DateTime? EmailedAt { get; set; }
    public DateTime? ViewedAt { get; set; }

    public PayrollRun? PayrollRun { get; set; }
    public List<PayslipLine> Lines { get; set; } = [];
}

/// <summary>
/// H6 (P9 step 9.7, DS9 PAYSLIP_LINE) — one line of one payslip, in payslip order.
/// <para>The lines carry their own GL account because <b>the payroll journal is built from them</b>: summing
/// the lines is what guarantees the journal agrees with the payslips it claims to represent, rather than two
/// independent calculations that can disagree.</para>
/// </summary>
public class PayslipLine : BaseEntity
{
    public string PayslipId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayslipLineType LineType { get; set; }
    public decimal Amount { get; set; }
    public int LineOrder { get; set; }

    /// <summary>How the figure was arrived at, in words — "30% of basic", "4 h at 1.5x". A payslip line nobody
    /// can explain is a payslip line that gets disputed.</summary>
    public string? Basis { get; set; }

    public bool IsTaxable { get; set; }
    public bool IsStatutory { get; set; }

    public string? SalaryComponentId { get; set; }
    public string? DeductionId { get; set; }

    /// <summary>Where this line posts. For an earning it is the expense account; for a deduction it is the
    /// liability the money is owed to; for an employer cost it is the EXPENSE account it is charged to.</summary>
    public string? GlAccountId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }

    /// <summary>
    /// The other side, used only by an employer contribution: it is one fact with two legs — a cost to the
    /// company (debit <see cref="GlAccountId"/>) and a liability to the same body the employee's own
    /// contribution is owed to (credit here).
    /// <para>Without this the employer half would debit and credit the SAME liability account, netting to
    /// nothing: the company's cost of employment would never reach the P&amp;L and the amount owed to NSSF
    /// would be understated by exactly the employer's share.</para>
    /// </summary>
    public string? ContraGlAccountId { get; set; }
    public string? ContraGlAccountCode { get; set; }
    public string? ContraGlAccountName { get; set; }

    public Payslip? Payslip { get; set; }
}

/// <summary>
/// H6 (P11, DS4 BANK_PAYMENT_FILE) — the salary payment file handed to a bank.
/// <para>The generated CSV is stored on the row rather than regenerated on download: the file the Finance
/// Manager approves must be byte-for-byte the file that reaches the bank portal, and regenerating it later
/// against changed bank details would quietly produce a different one.</para>
/// <para><see cref="NeedsFormatConfirmation"/> is the same honesty flag the seeded tax rates carry — the
/// column layouts here were written from the general shape of Kenyan bulk-salary uploads, not from the banks'
/// own template files, and must be checked against the real thing before a live upload.</para>
/// </summary>
public class BankPaymentFile : BaseEntity
{
    public string PayrollRunId { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;

    public BankFormat Format { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    /// <summary>The CSV itself. A payroll file for a company this size is a few kilobytes.</summary>
    public string Content { get; set; } = string.Empty;

    public int EmployeeCount { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>Staff left out because they have no primary bank account — named, never silently dropped.</summary>
    public string? MissingBankDetails { get; set; }
    public int MissingCount { get; set; }

    public bool NeedsFormatConfirmation { get; set; } = true;

    public string? GeneratedBy { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? DownloadedBy { get; set; }
    public DateTime? DownloadedAt { get; set; }

    /// <summary>Confirmed once the file has actually been uploaded to the bank — that is what triggers the
    /// second journal moving net pay out of the holding account and into the bank (P11 step 11.5).</summary>
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? BankGlAccountId { get; set; }
    public string? BankGlAccountCode { get; set; }
    public string? JournalEntryId { get; set; }
    public string? JournalEntryNo { get; set; }
    public string? JournalError { get; set; }

    public PayrollRun? PayrollRun { get; set; }
}
