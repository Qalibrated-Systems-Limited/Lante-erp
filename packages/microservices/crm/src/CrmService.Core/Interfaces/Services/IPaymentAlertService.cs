using CrmService.Core.DTOs.Payments;

namespace CrmService.Core.Interfaces.Services;

/// <summary>C13 (P14) — payment/debtor alerts. Reads the logged alerts the worker raises and computes a
/// live snapshot straight from the Finance service (scoped to the caller's tenant schema).</summary>
public interface IPaymentAlertService
{
    Task<List<PaymentAlertDto>> GetAlertsAsync(string? status, string? type, string? severity);
    Task<PaymentAlertSummaryDto> GetSummaryAsync(string schema);   // schema = caller's tenant, for Finance reads
    Task<PaymentAlertActionResult> AcknowledgeAsync(string id, string userId);
}
