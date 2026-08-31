namespace FinanceService.Core.DTOs;

public class AssetCategoryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AnnualRate { get; set; }
    public string UsefulLifeLabel { get; set; } = string.Empty;
}

public class CreateFixedAssetDto
{
    public string CategoryId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal AcquisitionCost { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public decimal? AnnualRateOverride { get; set; }
    public string? Location { get; set; }
    public string? LinkedTruckId { get; set; }
}

public class FixedAssetReadDto
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

public class DepreciationEntryReadDto
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

public class RunDepreciationResultDto
{
    public string Period { get; set; } = string.Empty;
    public int AssetsProcessed { get; set; }
    public decimal TotalCharge { get; set; }
    public string? JournalEntryId { get; set; }
    public bool AlreadyRun { get; set; }
}

public class CreateAssetDisposalDto
{
    public string AssetId { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal? Proceeds { get; set; }
}

public class AssetDisposalReadDto
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
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public bool RequiresBoardApproval { get; set; }
    public string? BoardApprovedBy { get; set; }
    public DateTime? BoardApprovedAt { get; set; }
}
