using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>Location-to-location movement request. Balance is only affected once approved —
/// approval re-validates available stock at FromLocationId before writing the two StockMovement
/// rows (TransferOut/TransferIn).</summary>
public class StoreTransfer : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string FromLocationId { get; set; } = string.Empty;
    public string ToLocationId { get; set; } = string.Empty;

    public decimal Qty { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public ItemMaster? Item { get; set; }
    public Location? FromLocation { get; set; }
    public Location? ToLocation { get; set; }
}
