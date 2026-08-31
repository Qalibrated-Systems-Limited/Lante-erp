namespace CrmService.Core.Entities;

/// <summary>P4 — PRICE_LIST (CRM-053). Standard rate card per service type with a discount limit,
/// the "agreed rate schedule" the SE reviews before quoting.</summary>
public class PriceList : BaseEntity
{
    public string ServiceType { get; set; } = string.Empty;
    public decimal StandardRate { get; set; }
    public decimal DiscountLimitPercent { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
