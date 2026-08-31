using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// Process 6 — a double-entry journal. Prepared → reviewed → approved (approval posts to the GL).
/// source_module/source_document_id trace an entry back to the module that raised it (backbone).
public class JournalEntry : BaseEntity
{
    public string EntryNo { get; set; } = string.Empty;       // e.g. "JV-2026-0001"
    public DateTime EntryDate { get; set; }
    public string PeriodId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CurrencyId { get; set; } = string.Empty;

    // Origin trace (ARCH-007). Null source_module = raised directly in Finance.
    public string? SourceModule { get; set; }
    public string? SourceDocumentId { get; set; }

    public JournalStatus Status { get; set; } = JournalStatus.Draft;

    // Reversal / accrual handling.
    public string? ReversalOfId { get; set; }
    public bool IsReversal { get; set; }
    public bool IsAccrual { get; set; }
    public DateTime? AutoReverseDate { get; set; }

    // Segregation of duties — three distinct users.
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? PostedAt { get; set; }

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}

/// A single balanced line of a journal. Stores original AND base-currency amounts (ARCH-003).
public class JournalLine : BaseEntity
{
    public string JournalEntryId { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string? CostCenterId { get; set; }
    public string? BranchId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public decimal FxRate { get; set; } = 1m;
    public decimal BaseDebit { get; set; }
    public decimal BaseCredit { get; set; }

    public JournalEntry? JournalEntry { get; set; }
}

/// Posted ledger row — written when a journal is approved. Drives TB / BS / P&L.
public class GeneralLedgerEntry : BaseEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string? CostCenterId { get; set; }
    public string? BranchId { get; set; }
    public string PeriodId { get; set; } = string.Empty;
    public string JournalEntryId { get; set; } = string.Empty;
    public string JournalLineId { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal BaseDebit { get; set; }
    public decimal BaseCredit { get; set; }
}

/// ARCH-007 — full audit trail on all financial transactions.
public class FinanceAuditLog : BaseEntity
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
