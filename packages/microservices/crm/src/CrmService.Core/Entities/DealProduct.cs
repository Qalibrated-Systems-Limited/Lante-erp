namespace CrmService.Core.Entities;

/// <summary>P6 — DEAL_PRODUCT. Products/services on a deal (copied from the linked quotation).</summary>
public class DealProduct : BaseEntity
{
    public string DealId { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public Deal? Deal { get; set; }
}
