using StoreService.Core.Enums;

namespace StoreService.Core.Entities;

/// <summary>Records goods arriving from a supplier. LandedCost is the TOTAL cost for the whole
/// line (purchase price + freight + customs/port clearance + inland transport, summed across all
/// QtyReceived units) — unit cost is derived as LandedCost / QtyReceived.</summary>
public class GoodsReceivedNote : BaseEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;

    /// <summary>Where the received stock lands — carried onto the generated StockUnit once passed.</summary>
    public string LocationId { get; set; } = string.Empty;

    public decimal QtyReceived { get; set; }

    /// <summary>Total landed cost for this receipt line (not per-unit).</summary>
    public decimal LandedCost { get; set; }

    public InspectionStatus InspectionStatus { get; set; } = InspectionStatus.Pending;
    public string? InspectedBy { get; set; }
    public DateTime? InspectedAt { get; set; }
    public string? Notes { get; set; }

    // ── Procurement Module 4 link (P5, DEC-3) ──
    // When a receipt is against a procurement LPO, these tie it back so a passed GRN closes the PO
    // line (via a callback to procurement) and feeds the P6 3-way match. Null for ad-hoc receipts.
    public string? PoId { get; set; }
    public string? PoNumber { get; set; }
    public decimal? AcceptedQty { get; set; }
    public decimal? RejectedQty { get; set; }
    public string? ConditionNotes { get; set; }
    public string? SerialNumber { get; set; }
    public string? DeliveryNoteUrl { get; set; }
    public string? SupplierInvoiceUrl { get; set; }
    /// <summary>True when this receipt does not fully satisfy the LPO — the PO stays open for the balance.</summary>
    public bool PartialDelivery { get; set; }

    public ItemMaster? Item { get; set; }
    public Supplier? Supplier { get; set; }
    public Location? Location { get; set; }
}
