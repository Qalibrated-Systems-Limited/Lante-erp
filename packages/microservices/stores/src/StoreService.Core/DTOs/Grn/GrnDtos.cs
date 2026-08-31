using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Grn;

public class GrnFilterParameters : PaginationParameters
{
    public string? ItemId { get; set; }
    public string? SupplierId { get; set; }
    public string? InspectionStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CreateGrnDto
{
    [Required]
    public string ItemId { get; set; } = string.Empty;

    [Required]
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>Where the received stock lands.</summary>
    [Required]
    public string LocationId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal QtyReceived { get; set; }

    /// <summary>Total landed cost for this receipt line (goods + freight + customs + transport).</summary>
    [Range(0, double.MaxValue)]
    public decimal LandedCost { get; set; }

    public string? Notes { get; set; }

    // ── P5 (DEC-3) — optional procurement LPO link ──
    public string? PoId { get; set; }
    public string? PoNumber { get; set; }
    public decimal? AcceptedQty { get; set; }
    public decimal? RejectedQty { get; set; }
    public string? ConditionNotes { get; set; }
    public string? SerialNumber { get; set; }
    public string? DeliveryNoteUrl { get; set; }
    public string? SupplierInvoiceUrl { get; set; }
    public bool PartialDelivery { get; set; }
}

/// <summary>Edits are only permitted while InspectionStatus is Pending — status transitions only
/// happen via the dedicated Inspect action.</summary>
public class UpdateGrnDto
{
    public decimal? QtyReceived { get; set; }
    public decimal? LandedCost { get; set; }
    public string? Notes { get; set; }
}

public class InspectGrnDto
{
    [Required]
    public bool Passed { get; set; }

    public string? Notes { get; set; }
}

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
