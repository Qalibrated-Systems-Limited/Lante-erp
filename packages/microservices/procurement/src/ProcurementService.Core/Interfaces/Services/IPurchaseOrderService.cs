using ProcurementService.Core.DTOs.PurchaseOrders;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P4 (LPO step 5) — LPO generation &amp; approval. Generated from an approved PR whose quotation
/// comparison is complete; routed through the value-based authority matrix (each step digitally signed);
/// LPOs &gt; 500k need a Board Resolution before the MD signs. Final approval posts the purchase-commitment
/// journal to Finance and issues the LPO.</summary>
public interface IPurchaseOrderService
{
    Task<PoListResult> GetAllAsync(PoFilterParams filter);
    Task<PoReadDto?> GetByIdAsync(string id);
    Task<PoReadDto?> GetByPrAsync(string prId);
    Task<PoActionResult> GenerateAsync(string prId, GenerateLpoDto dto, string userId);
    /// <summary>Restates the LPO's value (P7 converts a foreign price to KES) and re-derives the approval
    /// authority from it, since the matrix is value-based. Refuses once any step has been actioned or the LPO
    /// has left approval — the value an approver signed against must not change underneath them.</summary>
    Task<PoActionResult> RestateValueAsync(string poId, decimal newTotal, string currency, string userId);
    Task<PoActionResult> AttachBoardResolutionAsync(string poId, BoardResolutionDto dto, string userId);
    /// <summary><paramref name="viaEmergencyAuthorisation"/> is set only by the P8 emergency route: an
    /// emergency LPO cannot be approved through the ordinary sign path, because that would skip capturing the
    /// MD's mandatory authorisation reference. Rejection is always allowed here.</summary>
    Task<PoActionResult> SignAsync(string poId, SignLpoDto dto, string userId, string? userName, bool viaEmergencyAuthorisation = false);
    Task<PoActionResult> RecordReceiptAsync(string poId, RecordReceiptDto dto, string userId);
    Task<PoSummaryDto> GetSummaryAsync();
}
