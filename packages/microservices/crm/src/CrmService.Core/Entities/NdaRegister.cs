using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P13 (CRM-059) — NDA_REGISTER. Non-disclosure agreements with clients/partners; default 3-year
/// term, a 60-day-before-expiry alert goes to Legal + Head of BD.</summary>
public class NdaRegister : BaseEntity
{
    public string NdaNumber { get; set; } = string.Empty;      // NDA-{yr}-{seq}
    public string CounterpartyName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }                    // CRM customer, when the counterparty is one
    public string? Purpose { get; set; }
    public DateTime SignedDate { get; set; }
    public DateTime ExpiryDate { get; set; }                   // default SignedDate + 3 years
    public NdaStatus Status { get; set; } = NdaStatus.Active;
    public string? FileUrl { get; set; }
    public DateTime? ExpiryAlert60SentAt { get; set; }
}
