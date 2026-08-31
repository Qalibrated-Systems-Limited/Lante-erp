using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// Process 14 — staff imprest. Disbursement posts Dr Staff Imprest (1220) / Cr Cash. Retirement
/// moves the spent portion to expense. Any balance unretired after 14 days (FIN-012B) auto-converts
/// to a personal advance deducted from the next payroll (FIN-012C).
public class ImprestRequest : BaseEntity
{
    public string RefNo { get; set; } = string.Empty;      // IMP-2026-0001
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public ImprestStatus Status { get; set; } = ImprestStatus.Requested;

    public string? RequestedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public DateTime? DueDate { get; set; }                 // DisbursedAt + 14 days
    public decimal RetiredAmount { get; set; }
    public decimal UnretiredBalance { get; set; }
    public string ImprestAccountCode { get; set; } = "1220";
    public string BankAccountCode { get; set; } = "1100";
    public string? DisburseJournalEntryId { get; set; }

    public ICollection<ImprestRetirementLine> RetirementLines { get; set; } = new List<ImprestRetirementLine>();
}

public class ImprestRetirementLine : BaseEntity
{
    public string ImprestRequestId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptUrl { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string ExpenseAccountCode { get; set; } = "5500";
    public string? JournalEntryId { get; set; }
}

/// FIN-012C — the irreversible payroll deduction created when imprest isn't retired in time.
public class PersonalAdvance : BaseEntity
{
    public string ImprestRequestId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ConvertedAt { get; set; } = DateTime.UtcNow;
    public AdvanceStatus Status { get; set; } = AdvanceStatus.Pending;
    public string? DeductionMonth { get; set; }
    public string? Notes { get; set; }
}
