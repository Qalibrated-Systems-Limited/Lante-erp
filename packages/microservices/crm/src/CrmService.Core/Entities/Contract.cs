using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P6 — CONTRACT. Signed client contract registered against a deal, with renewal alerts at 60 and 30
/// days before end date (worker-guarded).
/// </summary>
public class Contract : BaseEntity
{
    public string ContractNumber { get; set; } = string.Empty;   // CON-{year}-{seq}
    public string DealId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContractType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Value { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal RetentionPct { get; set; }
    public string? FileUrl { get; set; }   // uploaded signed contract
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public DateTime? SignedAt { get; set; }

    // 60/30-day renewal alert guards.
    public DateTime? RenewalAlert60SentAt { get; set; }
    public DateTime? RenewalAlert30SentAt { get; set; }
}
