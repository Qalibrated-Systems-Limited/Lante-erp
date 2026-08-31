using ProcurementService.Core.DTOs.Quotations;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P3 — Quotation &amp; comparative analysis. The threshold band (from the PR total) sets the minimum
/// quotes; quotes may only come from ASR-approved suppliers; completing the comparison (received ≥ required)
/// records the recommended supplier and unblocks P4 LPO generation.</summary>
public interface IQuotationService
{
    Task<SourcingInfoDto> GetSourcingAsync(string prId);
    Task<ComparisonDto?> GetComparisonAsync(string prId);
    Task<QuotationActionResult> RecordQuotationAsync(string prId, RecordQuotationDto dto, string userId);
    Task<QuotationActionResult> ScoreQuotationAsync(string quotationId, ScoreQuotationDto dto, string userId);
    Task<QuotationActionResult> DeleteQuotationAsync(string quotationId, string userId);
    Task<QuotationActionResult> CompleteComparisonAsync(string prId, CompleteComparisonDto dto, string userId);
}
