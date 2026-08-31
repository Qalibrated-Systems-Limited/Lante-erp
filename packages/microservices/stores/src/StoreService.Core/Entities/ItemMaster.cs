using System.ComponentModel.DataAnnotations;

namespace StoreService.Core.Entities;

/// <summary>The central master record for every stocked item — code, category, pricing floors,
/// reorder thresholds, and the two system-maintained fields (AvgWeightedCost, QtyOnHand) that
/// GRN receipt / issue / sale flows keep in sync.</summary>
public class ItemMaster : BaseEntity
{
    [Required]
    public string SupplierId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string CategoryId { get; set; } = string.Empty;

    [Required]
    public string UomId { get; set; } = string.Empty;

    /// <summary>Either system-generated or captured by scanning a physical barcode reader
    /// into the field on the item form (readers act as keyboard input, no special
    /// integration needed). Nullable — not every item has one.</summary>
    [MaxLength(100)]
    public string? Barcode { get; set; }

    /// <summary>Minimum Selling Price — the profitability floor.</summary>
    public decimal MinSellingPrice { get; set; }

    public decimal MinStockLevel { get; set; }
    public decimal MaxStockLevel { get; set; }
    public decimal ReorderQty { get; set; }

    /// <summary>Perpetual weighted-average cost, recalculated on every GRN receipt.</summary>
    public decimal AvgWeightedCost { get; set; } = 0;

    /// <summary>System-maintained running quantity — incremented on GRN receipt, decremented on
    /// issue/sale, corrected on stock-take approval. Not client-settable.</summary>
    public decimal QtyOnHand { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    /// <summary>Set when a user dismisses the low-stock alert; auto-cleared once QtyOnHand rises
    /// back above MinStockLevel so a future dip re-alerts instead of staying silently suppressed.</summary>
    public DateTime? LowStockAcknowledgedAt { get; set; }
    public string? LowStockAcknowledgedBy { get; set; }

    public Supplier? Supplier { get; set; }
    public Category? Category { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    public virtual ICollection<ItemPhoto> Photos { get; set; } = new List<ItemPhoto>();
}

/// <summary>Item's own photo storage — previously these rode on operations-service's shared
/// Attachments feature (entityType="Item"), which coupled Stores' basic photo upload to a
/// different module's uptime and gateway routes. Mirrors Fleet's FieldVehiclePhoto.</summary>
public class ItemPhoto : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }

    public virtual ItemMaster? Item { get; set; }
}
