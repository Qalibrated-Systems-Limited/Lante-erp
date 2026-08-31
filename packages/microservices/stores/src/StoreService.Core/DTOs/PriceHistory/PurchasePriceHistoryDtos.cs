using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.PriceHistory;

public class PurchasePriceHistoryFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? SupplierId { get; set; }
    public string? AlertLevel { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>Standalone/manual purchase-price entry (e.g. a supplier quote not yet tied to a GRN) —
/// runs through the same 10%/25% variance red-flag logic as GRN-sourced entries.</summary>
public class CreatePurchasePriceHistoryDto
{
    [Required]
    public string ItemId { get; set; } = string.Empty;

    [Required]
    public string SupplierId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public DateTime? PurchasedOn { get; set; }
}

public class PurchasePriceHistoryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime PurchasedOn { get; set; }
    public decimal VariancePct { get; set; }
    public string AlertLevel { get; set; } = string.Empty;
    public string? SourceGrnId { get; set; }
    public DateTime CreatedAt { get; set; }
}
