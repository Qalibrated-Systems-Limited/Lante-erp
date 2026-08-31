namespace ReportingService.Core.DTOs;

// Mirrors FinanceService's FixedAssetReadDto, DepreciationEntryReadDto and AssetDisposalReadDto as
// returned by GET /api/v1/finance/fixed-assets, .../depreciation/schedule and .../disposals.
// Deliberately separate types rather than shared ones: reporting must not take a project reference on
// finance, and a mirrored DTO that drifts fails loudly at deserialisation rather than silently.

public class FixedAssetRowDto
{
    public string Id { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public decimal AnnualRate { get; set; }
    public string UsefulLifeLabel { get; set; } = string.Empty;
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public string? Location { get; set; }
    public string? LinkedTruckId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DepreciationEntryRowDto
{
    public string Id { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AccumulatedAfter { get; set; }
    public decimal NetBookValueAfter { get; set; }
    public string? JournalEntryId { get; set; }
}

public class AssetDisposalRowDto
{
    public string Id { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal? Proceeds { get; set; }
    public decimal ClosingNbv { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool RequiresBoardApproval { get; set; }
    public string? MdApprovedBy { get; set; }
    public string? BoardApprovedBy { get; set; }
}

/// <summary>
/// Report #11 — Fixed Asset Register &amp; Depreciation Schedule (#225). The register, the depreciation
/// charged, and disposals, in one place.
/// </summary>
public class FixedAssetRegisterReportDto
{
    public FixedAssetRegisterSummaryDto Summary { get; set; } = new();
    public List<FixedAssetCategoryTotalDto> ByCategory { get; set; } = new();
    public List<FixedAssetRowDto> Assets { get; set; } = new();
    public List<DepreciationEntryRowDto> DepreciationSchedule { get; set; } = new();
    public List<AssetDisposalRowDto> Disposals { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class FixedAssetRegisterSummaryDto
{
    public int AssetCount { get; set; }
    public decimal TotalAcquisitionCost { get; set; }
    public decimal TotalAccumulatedDepreciation { get; set; }
    public decimal TotalNetBookValue { get; set; }

    /// <summary>Depreciation charged in the period the schedule covers — NOT the accumulated total. The
    /// two are routinely confused and differ by orders of magnitude on an established register.</summary>
    public decimal DepreciationChargedInPeriod { get; set; }

    /// <summary>Set when the caller asked for a specific period, so a reader can tell whether
    /// <see cref="DepreciationChargedInPeriod"/> covers one month or the whole history.</summary>
    public string? Period { get; set; }

    public int DisposalCount { get; set; }
    public decimal DisposalProceeds { get; set; }

    /// <summary>Proceeds minus closing net book value, summed. Positive is a gain on disposal. Kept
    /// signed for the same reason variance is elsewhere: the sign is the whole message.</summary>
    public decimal DisposalGainOrLoss { get; set; }

    /// <summary>Disposals still awaiting MD or board approval. Worth surfacing on the register rather
    /// than only in finance: an asset pending disposal approval is still on the books, and its NBV is
    /// still counted above.</summary>
    public int DisposalsPendingApproval { get; set; }
}

public class FixedAssetCategoryTotalDto
{
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int AssetCount { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
}
