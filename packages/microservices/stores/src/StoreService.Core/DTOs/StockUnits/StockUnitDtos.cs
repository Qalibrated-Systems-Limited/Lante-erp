using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.StockUnits;

public class StockUnitFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? GrnId { get; set; }
    public string? Status { get; set; }
    public string? LocationId { get; set; }
}

/// <summary>Only status/location are ever mutated directly (e.g. moving a unit, marking it
/// write-off) — everything else is set at GRN-inspection time.</summary>
public class UpdateStockUnitDto
{
    public string? Status { get; set; }
    public string? LocationId { get; set; }
}

public class StockUnitReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string GrnId { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public decimal Qty { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
