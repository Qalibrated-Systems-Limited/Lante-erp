namespace ComplianceService.Core.Entities;

// ICM-004..010: a dual-ledger intercompany recharge posted under an ICSA — one entry on QSL's
// own ledger and one on the sister company's ledger. ReconciledAt null = still open; the
// background alert service fans out 30-day warning / 45-day critical reminders on age since
// PostedAt for anything unreconciled (see ComplianceAlertsBackgroundService).
public class IntercompanyTxn : BaseEntity
{
    public string IcsaId { get; set; } = string.Empty;
    public Icsa? Icsa { get; set; }
    public string RelatedPartyId { get; set; } = string.Empty;
    public RelatedParty? RelatedParty { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public string QslLedgerRef { get; set; } = string.Empty;
    public string SisterLedgerRef { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReconciledAt { get; set; }
}
