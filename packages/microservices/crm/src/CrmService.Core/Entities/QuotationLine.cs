namespace CrmService.Core.Entities;

/// <summary>P4 — QUOTATION_LINE. Line total = Quantity × UnitPrice × (1 − DiscountPercent/100).
/// ProductId references the Procurement PRODUCT catalogue (seam); free-text Description otherwise.</summary>
public class QuotationLine : BaseEntity
{
    public string QuotationId { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }

    public Quotation? Quotation { get; set; }
}
