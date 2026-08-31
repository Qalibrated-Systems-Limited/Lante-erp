using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// Processes 9/10 — bank reconciliation. A statement is matched line-by-line against the GL
/// movements on its bank account; unmatched statement lines (bank-only: charges, interest) are
/// posted as journals, and unmatched GL entries (book-only: deposits in transit, outstanding
/// cheques) form the reconciling items between the book and bank balances.
public class BankReconciliation : BaseEntity
{
    public string RefNo { get; set; } = string.Empty;         // REC-2026-0001
    public string BankAccountCode { get; set; } = "1100";
    public DateTime StatementDate { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public decimal BookBalance { get; set; }                  // GL balance of the bank account as of StatementDate
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Draft;
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
}

public class BankStatementLine : BaseEntity
{
    public string ReconciliationId { get; set; } = string.Empty;
    public DateTime TxnDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    /// Signed: positive = money into the bank (deposit/credit), negative = money out (withdrawal).
    public decimal Amount { get; set; }
    public StatementLineStatus MatchStatus { get; set; } = StatementLineStatus.Unmatched;
    public string? MatchedGlEntryId { get; set; }
    public string? JournalEntryId { get; set; }               // set when posted as a bank-only journal
}
