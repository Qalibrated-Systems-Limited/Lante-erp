using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>Immutable audit trail of every purchase price for an item+supplier pair, used to
/// red-flag 10%/25% price increases against the previous entry.</summary>
public class PurchasePriceHistory : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>Unit price for this purchase event (LandedCost / QtyReceived when sourced from a GRN).</summary>
    public decimal Price { get; set; }

    public DateTime PurchasedOn { get; set; } = DateTime.UtcNow;

    /// <summary>% change vs. the previous price for this item+supplier. 0 if this is the first entry.</summary>
    public decimal VariancePct { get; set; }

    public PriceAlertLevel AlertLevel { get; set; } = PriceAlertLevel.None;

    /// <summary>Nullable link back to the GRN that generated this row (null for manual/quote entries).</summary>
    public string? SourceGrnId { get; set; }

    public ItemMaster? Item { get; set; }
    public Supplier? Supplier { get; set; }
}
