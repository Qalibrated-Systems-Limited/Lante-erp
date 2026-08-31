namespace HrService.Core.DTOs.Payroll;

// ── Overtime (P12) ──
public class OvertimeRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime Date { get; set; }
    public decimal Hours { get; set; }
    public string RateType { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public string? PayrollRunId { get; set; }
    public decimal? PaidAmount { get; set; }
    /// <summary>"4.00 h on a public holiday at 2x" — the rule in words.</summary>
    public string Basis { get; set; } = string.Empty;
}

public class RequestOvertimeDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Hours { get; set; }
    public string? Reason { get; set; }
}

public class DecideOvertimeDto
{
    /// <summary>Approve | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

// ── Payroll run (P9) ──
public class PayrollRunDto
{
    public string Id { get; set; } = string.Empty;
    public string RunNumber { get; set; } = string.Empty;
    public string PayrollPeriodId { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? CutOffDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "KES";

    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalTaxable { get; set; }
    public decimal TotalPaye { get; set; }
    public decimal TotalStatutory { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerCost { get; set; }

    public string? ComputedBy { get; set; }
    public DateTime? ComputedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public string? JournalEntryId { get; set; }
    public string? JournalEntryNo { get; set; }
    public DateTime? JournalPostedAt { get; set; }
    public string? JournalError { get; set; }

    /// <summary>Employees left out of the run and why — a short run must be explicable.</summary>
    public List<string> Exclusions { get; set; } = [];
    public string? Notes { get; set; }
    public List<PayslipDto> Payslips { get; set; } = [];
}

public class PayslipDto
{
    public string Id { get; set; } = string.Empty;
    public string PayrollRunId { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentName { get; set; }
    public string? KraPin { get; set; }
    public string? SalaryStructureName { get; set; }

    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public decimal GrossPay { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal Paye { get; set; }
    public decimal PersonalRelief { get; set; }
    public decimal StatutoryDeductions { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal EmployerCost { get; set; }

    public decimal OvertimeHours { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal UnpaidDays { get; set; }
    public decimal UnpaidDeduction { get; set; }

    public string? PdfUrl { get; set; }
    public DateTime? EmailedAt { get; set; }
    public DateTime? ViewedAt { get; set; }

    public List<PayslipLineDto> Lines { get; set; } = [];
}

public class PayslipLineDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LineType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int LineOrder { get; set; }
    public string? Basis { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsStatutory { get; set; }
    public string? GlAccountCode { get; set; }
    public string? GlAccountName { get; set; }
}

public class CreatePayrollRunDto
{
    /// <summary>Defaults to the current open period.</summary>
    public string? PayrollPeriodId { get; set; }
    public string? Notes { get; set; }
}

public class DecidePayrollRunDto
{
    /// <summary>Approve | Cancel.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>
    /// Set when approving a run computed from statutory rates that carry <c>NeedsConfirmation</c>. Approval
    /// is refused without it, and setting it records against the approver's name that they accepted
    /// unverified figures. Defaults to false so it is never acknowledged by omission (#244).
    /// </summary>
    public bool AcknowledgeUnconfirmedRates { get; set; }
}

/// <summary>One line of the journal a run would post, so it can be inspected BEFORE approval commits it.</summary>
public class JournalPreviewLineDto
{
    /// <summary>The account the line posts to. This is the identifier the journal is built and posted
    /// with; <see cref="AccountCode"/> is for display. Keeping both and validating one while posting the
    /// other is what #255 was.</summary>
    public string AccountId { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class JournalPreviewDto
{
    public string RunNumber { get; set; } = string.Empty;
    public List<JournalPreviewLineDto> Lines { get; set; } = [];
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool Balances { get; set; }
    /// <summary>Anything that would stop the posting — an unmapped component, a missing holding account.</summary>
    public List<string> Problems { get; set; } = [];
}

// ── Bank payment files (P11) ──
public class BankPaymentFileDto
{
    public string Id { get; set; } = string.Empty;
    public string PayrollRunId { get; set; } = string.Empty;
    public string PayrollPeriodCode { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int MissingCount { get; set; }
    /// <summary>Staff with no primary bank account — named so they can be fixed and paid, never silently dropped.</summary>
    public List<string> MissingBankDetails { get; set; } = [];
    /// <summary>True until someone has checked this layout against the bank's own template.</summary>
    public bool NeedsFormatConfirmation { get; set; }
    public string? GeneratedBy { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? DownloadedAt { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? BankGlAccountCode { get; set; }
    public string? JournalEntryNo { get; set; }
    public string? JournalError { get; set; }
}

public class GenerateBankFileDto
{
    /// <summary>Generic | Kcb | Equity | Ncba | CoOp.</summary>
    public string Format { get; set; } = "Generic";
}

public class ConfirmBankFileDto
{
    /// <summary>The bank account the salaries actually left — this is the credit side of the second journal.</summary>
    public string BankGlAccountId { get; set; } = string.Empty;
    public string? Reference { get; set; }
}

// ── P9 annual tax certificate (HR-010) ──
public class P9MonthDto
{
    public string PeriodCode { get; set; } = string.Empty;
    public decimal GrossPay { get; set; }
    public decimal TaxableIncome { get; set; }
    /// <summary>Tax before relief — PAYE plus the relief that was applied.</summary>
    public decimal TaxCharged { get; set; }
    public decimal PersonalRelief { get; set; }
    public decimal Paye { get; set; }
    public decimal StatutoryDeductions { get; set; }
}

public class P9CertificateDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? KraPin { get; set; }
    public int Year { get; set; }
    public List<P9MonthDto> Months { get; set; } = [];
    public decimal TotalGross { get; set; }
    public decimal TotalTaxable { get; set; }
    public decimal TotalTaxCharged { get; set; }
    public decimal TotalRelief { get; set; }
    public decimal TotalPaye { get; set; }
    public decimal TotalStatutory { get; set; }
}
