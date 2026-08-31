namespace ReportingService.Core.DTOs;

// Mirrors StoreService.Core.DTOs shapes returned by GET /api/v1/grn, /api/v1/suppliers,
// /api/v1/items. Each GRN row is already a single item/supplier/LandedCost line — there is no
// separate "GRN line item" collection to unpack.

public class GrnReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public decimal QtyReceived { get; set; }
    public decimal LandedCost { get; set; }
    public decimal UnitCost { get; set; }
    public string InspectionStatus { get; set; } = string.Empty;
    public string? InspectedBy { get; set; }
    public DateTime? InspectedAt { get; set; }
    public string? Notes { get; set; }
    public decimal? VariancePct { get; set; }
    public string? AlertLevel { get; set; }
    public int StockUnitCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SupplierReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public decimal? Rating { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
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
    public string Uom { get; set; } = string.Empty;
    public decimal AvgWeightedCost { get; set; }
    public decimal QtyOnHand { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
