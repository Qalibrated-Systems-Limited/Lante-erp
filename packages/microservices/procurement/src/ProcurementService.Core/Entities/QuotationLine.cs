namespace ProcurementService.Core.Entities;

/// <summary>P3 — QUOTATION_LINE. A single priced item on a supplier's quote.</summary>
public class QuotationLine : BaseEntity
{
    public string QuotationId { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Quotation? Quotation { get; set; }
}
