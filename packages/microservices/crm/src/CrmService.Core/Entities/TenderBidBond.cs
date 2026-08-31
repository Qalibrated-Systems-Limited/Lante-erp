using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P5 — TENDER_BID_BOND (CRM-020). Bank guarantee for a tender; a 14-day expiry alert fires
/// before ValidityDate.</summary>
public class TenderBidBond : BaseEntity
{
    public string TenderId { get; set; } = string.Empty;
    public string GuaranteeNumber { get; set; } = string.Empty;
    public string IssuingBank { get; set; } = string.Empty;
    public DateTime ValidityDate { get; set; }
    public decimal Amount { get; set; }
    public BidBondStatus Status { get; set; } = BidBondStatus.Active;
    public DateTime? ExpiryAlertSentAt { get; set; }

    public Tender? Tender { get; set; }
}
