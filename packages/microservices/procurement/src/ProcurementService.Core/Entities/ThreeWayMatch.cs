using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P6 — the 3-way match record for an LPO (final gate before payment). Compares the PO, the goods received
/// (GRN accepted qty, synced from Stores in P5) and the supplier invoice (read from Finance). A clean match
/// hands a payment voucher to Finance; discrepancies raise <see cref="MatchingException"/> rows. Header/value
/// level (the PO is header-level — no PO lines), so checks are receipt-complete, invoice-present, and
/// invoice-total vs PO-total.
/// </summary>
public class ThreeWayMatch : BaseEntity
{
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;

    public string? SupplierInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }

    public decimal PoTotal { get; set; }
    public decimal InvoiceTotal { get; set; }
    public decimal ReceivedQty { get; set; }

    public bool ReceivedOk { get; set; }
    public bool PriceOk { get; set; }
    public bool TotalOk { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Pending;

    public string? PaymentVoucherRef { get; set; }
    public string? PaymentVoucherNo { get; set; }

    public string? MatchedBy { get; set; }
    public DateTime? MatchedAt { get; set; }
}
