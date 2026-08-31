namespace StoreService.Core.Entities;

/// <summary>Permanent record of every item sold — client, serial number, price, invoice. Feeds
/// Finance for COGS via the CostAtSale snapshot (immutable, taken at sale time).</summary>
public class SoldItem : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string StockUnitId { get; set; } = string.Empty;

    /// <summary>External/CRM client id — no local FK constraint (cross-service reference).</summary>
    public string ClientId { get; set; } = string.Empty;

    public string? SerialNo { get; set; }

    /// <summary>Quantity sold in this transaction — a bulk lot can be sold across several
    /// transactions, so this may be less than the stock unit's original received quantity.</summary>
    public decimal Qty { get; set; } = 1;

    /// <summary>Total sale amount for Qty (not a per-unit price).</summary>
    public decimal SalePrice { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SoldOn { get; set; } = DateTime.UtcNow;

    /// <summary>Snapshot of ItemMaster.AvgWeightedCost × Qty at the moment of sale — immutable for
    /// COGS reporting (a total for this transaction, not a per-unit cost).</summary>
    public decimal CostAtSale { get; set; }

    public ItemMaster? Item { get; set; }
    public StockUnit? StockUnit { get; set; }
}
