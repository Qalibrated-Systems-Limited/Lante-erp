namespace FinanceService.Core.Entities;

/// One of the 7 QSL asset-category rows (spec §5) — annual straight-line rate + display label.
public class AssetCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;            // "Motor Vehicles", "IT Equipment & Computers", ...
    public decimal AnnualRate { get; set; }                      // 0 for "Over lease term" categories
    public string UsefulLifeLabel { get; set; } = string.Empty;  // "5 years" / "Over lease term" (display)
}

public class FixedAsset : BaseEntity
{
    public string CategoryId { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;         // auto: FA-{year}-{seq}, unique
    public string Description { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public decimal? AnnualRateOverride { get; set; }              // for Leasehold Improvements (per-lease rate)
    public decimal AccumulatedDepreciation { get; set; } = 0;
    public string? Location { get; set; }                         // office/site/vehicle (ASSET-006)
    public string? LinkedTruckId { get; set; }                    // Fleet integration (ASSET-008) — plain cross-service reference
    public string Status { get; set; } = "Active";                // Active | Disposed

    public virtual AssetCategory? Category { get; set; }
    public virtual ICollection<DepreciationEntry> DepreciationEntries { get; set; } = new List<DepreciationEntry>();
}

public class DepreciationEntry : BaseEntity
{
    public string AssetId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;            // "2026-07"
    public decimal Amount { get; set; }
    public decimal AccumulatedAfter { get; set; }
    public decimal NetBookValueAfter { get; set; }
    public string? JournalEntryId { get; set; }                   // link to the posted Journal

    public virtual FixedAsset? Asset { get; set; }
}

public class AssetDisposal : BaseEntity
{
    public string AssetId { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public string Method { get; set; } = string.Empty;            // Sold | Scrapped | Donated | WrittenOff
    public decimal? Proceeds { get; set; }
    public decimal ClosingNbv { get; set; }
    public string Status { get; set; } = "PendingMdApproval";     // PendingMdApproval | PendingBoardApproval | Approved | Rejected
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public bool RequiresBoardApproval { get; set; }               // true when asset value > Kshs 200,000
    public string? BoardApprovedBy { get; set; }                  // MD records this once the Board has signed off offline
    public DateTime? BoardApprovedAt { get; set; }

    public virtual FixedAsset? Asset { get; set; }
}
