using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>Immutable audit-trail row for every signed quantity delta an item has ever undergone
/// at a location. Per-location balance = SUM(Quantity) grouped by ItemId+LocationId — this is the
/// single source of truth the reference calls "Movement"/"Balance".</summary>
public class StockMovement : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;

    public MovementType Type { get; set; }

    /// <summary>Signed delta — positive for receipts/transfer-in, negative for issues/sales/transfer-out,
    /// either sign for adjustments.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Running balance for this item+location immediately after this row was written.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Id of the GRN/SIN/SoldItem/Transfer/StockTake that caused this movement.</summary>
    public string Reference { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public ItemMaster? Item { get; set; }
    public Location? Location { get; set; }
}
