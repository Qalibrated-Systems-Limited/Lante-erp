namespace ProcurementService.Core.Entities;

/// <summary>
/// P7 — CUSTOMS_DECLARATION. The import entry lodged for a shipment (IDF number and the KES charges levied
/// at clearance). This is the source document; lodging it creates/refreshes the matching
/// <see cref="LandedCostComponent"/> rows (import duty, clearing agent fee, port charges) so the landed cost
/// has a single summation path and customs charges are never counted twice. One declaration per order.
/// </summary>
public class CustomsDeclaration : BaseEntity
{
    public string IntlPoId { get; set; } = string.Empty;
    public string IdfNumber { get; set; } = string.Empty;
    public string? EntryNumber { get; set; }

    public decimal ImportDutyKes { get; set; }
    public decimal ClearingAgentFeeKes { get; set; }
    public decimal PortChargesKes { get; set; }

    public DateTime DeclaredAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
