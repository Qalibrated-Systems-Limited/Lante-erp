using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>Tracks an individual unit (or a bulk lot, for non-serialized items) received via a
/// specific GRN, through its lifecycle to issue/sale/write-off.</summary>
public class StockUnit : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string GrnId { get; set; } = string.Empty;

    /// <summary>Null for bulk/non-serialized items that track quantity only.</summary>
    public string? SerialNo { get; set; }

    /// <summary>Quantity still remaining in this lot — set to the GRN's QtyReceived when created,
    /// decremented by each partial issue/sale. Status only flips to Issued/Sold once this hits 0,
    /// so a partially-consumed bulk lot stays InStock and sellable/issuable for the remainder.</summary>
    public decimal Qty { get; set; }

    public StockUnitStatus Status { get; set; } = StockUnitStatus.InStock;
    public string? LocationId { get; set; }

    public ItemMaster? Item { get; set; }
    public GoodsReceivedNote? Grn { get; set; }
    public Location? Location { get; set; }
}
