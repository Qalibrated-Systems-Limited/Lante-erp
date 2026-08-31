using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.SoldItems;

public class SoldItemFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? ClientId { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CreateSoldItemDto
{
    [Required]
    public string ItemId { get; set; } = string.Empty;

    [Required]
    public string StockUnitId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Quantity being sold from the stock unit's remaining lot — must not exceed it.</summary>
    [Range(0.01, double.MaxValue)]
    public decimal Qty { get; set; } = 1;

    /// <summary>Total sale amount for Qty (not a per-unit price).</summary>
    [Range(0.01, double.MaxValue)]
    public decimal SalePrice { get; set; }

    [Required]
    public string InvoiceNo { get; set; } = string.Empty;
}

public class SoldItemReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string StockUnitId { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public decimal Qty { get; set; }
    public decimal SalePrice { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SoldOn { get; set; }
    public decimal CostAtSale { get; set; }
    public decimal GrossMargin => SalePrice - CostAtSale;
    public DateTime CreatedAt { get; set; }
}
