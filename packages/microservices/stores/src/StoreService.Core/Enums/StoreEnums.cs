namespace StoreService.Core.Enums;

/// <summary>GRN inspection workflow state — StockUnits are only generated once a GRN passes inspection.</summary>
public enum InspectionStatus
{
    Pending = 0,
    Passed = 1,
    Failed = 2,
}

/// <summary>Lifecycle state of a single tracked stock unit.</summary>
public enum StockUnitStatus
{
    InStock = 0,
    Issued = 1,
    Sold = 2,
    Reserved = 3,
    WrittenOff = 4,
}

/// <summary>Why stock left the store via a Store Issue Note.</summary>
public enum IssueType
{
    Sale = 0,
    Internal = 1,
}

/// <summary>Purchase-price red-flag severity vs. the previous purchase for the same item+supplier.</summary>
public enum PriceAlertLevel
{
    None = 0,
    Warning = 1,   // >= 10% increase
    Critical = 2,  // >= 25% increase
}

/// <summary>Stock-take reconciliation workflow state.</summary>
public enum StockTakeApprovalStatus
{
    Pending = 0,
    Approved = 1,
}

/// <summary>Kind of physical place stock can sit — matches the QSL reference's location types.</summary>
public enum LocationType
{
    Warehouse = 0,
    Site = 1,
    Vehicle = 2,
    Vendor = 3,
}

/// <summary>Every signed quantity delta that has ever moved an item at a location — the single
/// source of truth for per-location balances (SUM(Quantity) grouped by ItemId+LocationId).</summary>
public enum MovementType
{
    Receipt = 0,
    Issue = 1,
    Sale = 2,
    TransferOut = 3,
    TransferIn = 4,
    Adjustment = 5,
}

/// <summary>Location-to-location stock transfer workflow state.</summary>
public enum TransferStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
}

/// <summary>Why a stock-take/adjustment variance exists — merges the reference's separate
/// "Adjustments" reason taxonomy into StockTakeReconciliation.</summary>
public enum AdjustmentReasonCode
{
    CountCorrection = 0,
    Damage = 1,
    Expiry = 2,
    Theft = 3,
    WriteOff = 4,
    Other = 5,
}
