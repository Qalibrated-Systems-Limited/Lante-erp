namespace OperationsService.Core.Entities;

/// <summary>O7 — a line item on a variation order (Amount = Quantity × UnitPrice).</summary>
public class VariationOrderLine : BaseEntity
{
    public string  VariationOrderId { get; set; } = string.Empty;
    public string  Description      { get; set; } = string.Empty;
    public decimal Quantity         { get; set; } = 1;
    public decimal UnitPrice        { get; set; }
    public decimal Amount           { get; set; }

    public VariationOrder VariationOrder { get; set; } = null!;
}
