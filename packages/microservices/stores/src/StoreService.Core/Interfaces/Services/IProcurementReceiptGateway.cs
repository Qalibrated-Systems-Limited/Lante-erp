namespace StoreService.Core.Interfaces.Services;

/// <summary>P5 (DEC-3) — outbound seam to Procurement (Module 4). When a GRN raised against a procurement
/// LPO passes inspection, notify procurement so it closes/updates the PO line (received quantity, partial
/// flag). Config-gated no-op when Procurement isn't wired; never throws (a receipt must not fail on the
/// callback).</summary>
public record PoReceiptNotice(
    string PoId,
    decimal AcceptedQty,
    decimal RejectedQty,
    bool PartialDelivery,
    string GrnId);

public interface IProcurementReceiptGateway
{
    Task NotifyReceiptAsync(PoReceiptNotice notice, CancellationToken ct = default);
}
