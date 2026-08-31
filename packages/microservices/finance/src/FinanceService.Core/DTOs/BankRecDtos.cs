namespace FinanceService.Core.DTOs;

public class CreateReconciliationDto
{
    public string? BankAccountCode { get; set; }              // defaults 1100
    public DateTime StatementDate { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public string? Notes { get; set; }
    public List<StatementLineDto> Lines { get; set; } = new();
}

public class StatementLineDto
{
    public DateTime TxnDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }                       // signed, + into bank
}

public class PostBankItemDto
{
    /// Contra account for the bank-only item (e.g. 5500 bank charges, or 4000-series interest income).
    public string ContraAccountCode { get; set; } = string.Empty;
}

public class ReconciliationLineDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime TxnDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string MatchStatus { get; set; } = string.Empty;
    public string? MatchedGlEntryId { get; set; }
}

public class GlEntryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public decimal Amount { get; set; }                       // signed, debit − credit (+ into bank)
    public string? JournalEntryId { get; set; }
    public string? Description { get; set; }
    public bool Matched { get; set; }
}

public class ReconciliationReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public string BankAccountCode { get; set; } = string.Empty;
    public DateTime StatementDate { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public decimal BookBalance { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Reconciliation summary (classic two-column identity).
    public decimal AdjustedBookBalance { get; set; }          // book + bank-only items (once posted)
    public decimal AdjustedBankBalance { get; set; }          // statement + book-only reconciling items
    public decimal Difference { get; set; }                   // adjustedBank − adjustedBook; 0 when reconciled
    public int MatchedCount { get; set; }
    public int UnmatchedStatementCount { get; set; }
    public int UnmatchedBookCount { get; set; }

    public List<ReconciliationLineDto> Lines { get; set; } = new();
    public List<GlEntryDto> UnmatchedBookEntries { get; set; } = new();
}

public class ReconciliationSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public string BankAccountCode { get; set; } = string.Empty;
    public DateTime StatementDate { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = string.Empty;
}
