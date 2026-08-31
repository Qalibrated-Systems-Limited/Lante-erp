namespace CrmService.Core.Entities;

/// <summary>
/// P1 — CLIENT_CREDIT_LIMIT (CRM-005). CFO-approved credit limit + terms, recorded at onboarding
/// (and on later revisions). Finance alerts when a client's outstanding balance exceeds the limit
/// (P14 seam). The current values are also denormalised onto <see cref="Customer"/> for fast reads.
/// </summary>
public class ClientCreditLimit : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public int CreditTermsDays { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;   // CFO
    public DateTime SetAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
