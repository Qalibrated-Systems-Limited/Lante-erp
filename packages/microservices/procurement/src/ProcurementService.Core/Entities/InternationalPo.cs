using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P7 (PROC-003) — INTERNATIONAL_PO. The foreign-sourcing detail of an existing LPO (1:1 on
/// <see cref="PoId"/>), so international orders reuse the whole PR → comparison → LPO → authority-matrix →
/// GRN → 3-way-match spine rather than running a parallel one. Carries the FX terms, the T/T advance and its
/// mandatory MD approval, the shipment reference, and the landed-cost roll-up.
/// <para>Landed cost = (purchase price + freight + import duty + clearing agent fees + port and handling)
/// ÷ quantity received, every component converted to KES at the rate prevailing when that cost was incurred
/// (so each <see cref="LandedCostComponent"/> keeps its own rate, not this order's).</para>
/// </summary>
public class InternationalPo : BaseEntity
{
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }

    // ── FX terms at order date ──
    public string CurrencyCode { get; set; } = string.Empty;     // e.g. CNY, USD
    public decimal PurchasePriceFx { get; set; }
    /// <summary>KES per 1 unit of <see cref="CurrencyCode"/>, snapshotted when the order was placed.</summary>
    public decimal ExchangeRateAtOrder { get; set; }
    public decimal PurchasePriceKes { get; set; }
    public string? ProformaInvoiceUrl { get; set; }

    // ── T/T advance (mandatory MD approval before funds move — PROC-003 key control) ──
    public decimal? TtAmountFx { get; set; }
    public DateTime? TtRequestedAt { get; set; }
    public string? TtApprovedBy { get; set; }
    public DateTime? TtApprovedAt { get; set; }
    public DateTime? TtSentAt { get; set; }
    /// <summary>Finance ad-hoc payment voucher raised for the advance (Finance disburses — DEC-4/DEC-A).</summary>
    public string? TtVoucherRef { get; set; }
    public string? TtVoucherNo { get; set; }

    // ── Shipment ──
    public string? BlNumber { get; set; }        // bill of lading / airway bill
    public DateTime? Eta { get; set; }

    // ── Landed cost roll-up (recomputed whenever a component or the customs entry changes) ──
    public decimal TotalLandedCostKes { get; set; }
    public decimal LandedCostPerUnitKes { get; set; }
    /// <summary>Quantity the per-unit cost was divided by — the LPO's received quantity once Stores has
    /// confirmed receipt (P5 callback), otherwise the ordered quantity captured here.</summary>
    public decimal QuantityBasis { get; set; }

    public IntlPoStatus Status { get; set; } = IntlPoStatus.Draft;

    public ICollection<LandedCostComponent> Components { get; set; } = new List<LandedCostComponent>();
}
