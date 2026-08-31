using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.StockTake;

public class StockTakeFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CreateStockTakeDto
{
    [Required]
    public string ItemId { get; set; } = string.Empty;

    public string? StockUnitId { get; set; }

    /// <summary>Which location this physical count/adjustment applies to.</summary>
    [Required]
    public string LocationId { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal PhysicalCount { get; set; }

    /// <summary>Defaults to CountCorrection (a routine physical-count session) if not given —
    /// set to Damage/Expiry/Theft/WriteOff/Other for an ad-hoc reason-coded adjustment.</summary>
    public string? ReasonCode { get; set; }
}

public class ApproveStockTakeDto
{
    [Required]
    public string ApprovedBy { get; set; } = string.Empty;
}

public class StockTakeReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string? StockUnitId { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public decimal PhysicalCount { get; set; }
    public decimal SystemCount { get; set; }
    public decimal Variance { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
