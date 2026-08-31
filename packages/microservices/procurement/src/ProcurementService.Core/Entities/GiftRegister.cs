namespace ProcurementService.Core.Entities;

/// <summary>P1 (PROC-007, anti-bribery) — GIFT_REGISTER. Every gift/hospitality a staff member receives
/// from a supplier must be declared, with an optional link to an active procurement event (PO) so a
/// declaration can be correlated with live sourcing.</summary>
public class GiftRegister : BaseEntity
{
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;      // staff user id
    public string? ReceivedByName { get; set; }
    public string GiftDescription { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public DateTime DeclaredAt { get; set; } = DateTime.UtcNow;
    public string? LinkedPoId { get; set; }                     // active PO (P4+); nullable for now
}
