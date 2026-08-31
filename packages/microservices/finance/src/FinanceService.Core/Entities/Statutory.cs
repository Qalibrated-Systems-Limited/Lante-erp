namespace FinanceService.Core.Entities;

/// FIN-025 — a statutory remittance (PAYE / NSSF / SHA / Housing Levy / WHT / VAT) paid to the
/// relevant authority for a given month. Posts Dr <liability> / Cr Bank when recorded.
public class StatutoryRemittance : BaseEntity
{
    public string RefNo { get; set; } = string.Empty;         // STAT-2026-0001
    public string ObligationCode { get; set; } = string.Empty; // liability account code, e.g. 2210
    public string ObligationName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;        // yyyy-MM the deductions relate to
    public decimal Amount { get; set; }
    public string BankAccountCode { get; set; } = "1100";
    public DateTime DueDate { get; set; }
    public DateTime RemittedAt { get; set; } = DateTime.UtcNow;
    public string? PaymentReference { get; set; }             // iTax/eCitizen ref (stub)
    public string? JournalEntryId { get; set; }
}
