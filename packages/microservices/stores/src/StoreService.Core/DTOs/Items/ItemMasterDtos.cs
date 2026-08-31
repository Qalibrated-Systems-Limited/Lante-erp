using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Items;

public class ItemMasterFilterParameters : PaginationParameters
{
    public string? SupplierId { get; set; }
    public string? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool? LowStockOnly { get; set; }
}

public class CreateItemMasterDto
{
    [Required]
    public string SupplierId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ItemCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required]
    public string CategoryId { get; set; } = string.Empty;

    [Required]
    public string UomId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Barcode { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MinSellingPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MinStockLevel { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxStockLevel { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ReorderQty { get; set; }
}

/// <summary>All-nullable — null means "leave unchanged". Never exposes AvgWeightedCost or
/// QtyOnHand; those are system-maintained by GRN/issue/sale/stock-take flows only.</summary>
public class UpdateItemMasterDto
{
    public string? SupplierId { get; set; }
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public string? CategoryId { get; set; }
    public string? UomId { get; set; }
    public string? Barcode { get; set; }
    public decimal? MinSellingPrice { get; set; }
    public decimal? MinStockLevel { get; set; }
    public decimal? MaxStockLevel { get; set; }
    public decimal? ReorderQty { get; set; }
    public bool? IsActive { get; set; }
}

public class ItemMasterReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
    public string Uom { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal MinSellingPrice { get; set; }
    public decimal MinStockLevel { get; set; }
    public decimal MaxStockLevel { get; set; }
    public decimal ReorderQty { get; set; }
    public decimal AvgWeightedCost { get; set; }
    public decimal QtyOnHand { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsLowStockAcknowledged { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
