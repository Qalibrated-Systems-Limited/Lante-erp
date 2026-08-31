namespace StoreService.Core.Entities;

/// <summary>P5 (DFD 5.2a) — GOODS_REJECTION_NOTE. Raised when received goods fail inspection: the items are
/// quarantined, photographed and the originating LPO's supplier is notified. Linked to the procurement LPO
/// so the PO stays open for a replacement delivery.</summary>
public class GoodsRejectionNote : BaseEntity
{
    public string? GrnId { get; set; }
    public string? PoId { get; set; }
    public string? PoNumber { get; set; }
    public string SupplierId { get; set; } = string.Empty;
    public string? ItemId { get; set; }
    public decimal RejectedQty { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? PhotoUrls { get; set; }        // comma-separated / JSON
    public string? RejectedItems { get; set; }
    public DateTime? NotifiedAt { get; set; }
}
