using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Movements;

public class StockMovementFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? LocationId { get; set; }
    public string? Type { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class StockMovementReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

/// <summary>Per-location breakdown for a single item — the "Balances by Location" view.</summary>
public class ItemLocationBalanceDto
{
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public class ItemBalancesDto
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public List<ItemLocationBalanceDto> Balances { get; set; } = new();
}
