namespace ProcurementService.Core.Entities;

/// <summary>P2 — PURCHASE_REQUISITION_LINE. A single requested item on a PR.</summary>
public class PurchaseRequisitionLine : BaseEntity
{
    public string PrId { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public PurchaseRequisition? Pr { get; set; }
}
