using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>SIN — controls what leaves the store for internal use (not a sale).</summary>
public class StoreIssueNote : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Nullable — a bulk issue may not target one serialized unit.</summary>
    public string? StockUnitId { get; set; }

    /// <summary>Resolved from the stock unit, or given explicitly for bulk issues.</summary>
    public string? LocationId { get; set; }

    public decimal QtyIssued { get; set; }

    public string CostCenter { get; set; } = string.Empty;
    public IssueType IssueType { get; set; }
    public string IssuedTo { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; } = DateTime.UtcNow;

    public ItemMaster? Item { get; set; }
    public StockUnit? StockUnit { get; set; }
    public Location? Location { get; set; }
}
