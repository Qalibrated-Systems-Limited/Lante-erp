using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>Used during physical counts to find variances between physical and system stock at a
/// given location. Approval writes an Adjustment StockMovement for the variance and shifts
/// ItemMaster.QtyOnHand (the all-locations total) by that same delta — PhysicalCount/SystemCount
/// are location-scoped, so QtyOnHand can't simply be set to PhysicalCount.</summary>
public class StockTakeReconciliation : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Nullable — bulk items reconcile by quantity, not per serialized unit.</summary>
    public string? StockUnitId { get; set; }

    public decimal PhysicalCount { get; set; }
    public decimal SystemCount { get; set; }

    public decimal Variance => PhysicalCount - SystemCount;

    /// <summary>Which location this count/adjustment applies to — merges the reference's separate
    /// "Adjustments" concept into this entity instead of a parallel workflow.</summary>
    public string? LocationId { get; set; }
    public AdjustmentReasonCode ReasonCode { get; set; } = AdjustmentReasonCode.CountCorrection;

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public StockTakeApprovalStatus Status { get; set; } = StockTakeApprovalStatus.Pending;

    public ItemMaster? Item { get; set; }
    public StockUnit? StockUnit { get; set; }
    public Location? Location { get; set; }
}
