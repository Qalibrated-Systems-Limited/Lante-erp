using ProcurementService.Core.DTOs.Performance;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P9 (PROC-002) — biannual supplier performance review. Scores every approved, non-blacklisted
/// supplier from transaction data already in the system (Quality 30 / Delivery 25 / Pricing 25 /
/// Compliance 20) and feeds the result back into the ASR: it ranks suppliers in the P3 comparison and, below
/// 40, escalates to the MD, who may then blacklist through P1. The review itself never blacklists.</summary>
public interface IPerformanceReviewService
{
    Task<PerformanceListResult> GetAllAsync(PerformanceFilterParams filter);
    Task<PerformanceReviewDto?> GetByIdAsync(string id);
    Task<List<PerformanceRowDto>> GetHistoryAsync(string supplierId);
    Task<PerformanceSummaryDto> GetSummaryAsync(string? period);

    /// <summary>Scores every eligible supplier for the period (idempotent — re-running refreshes the review).</summary>
    Task<PerformanceActionResult> RunAsync(RunReviewDto dto, string userId);
    /// <summary>Scores one supplier for the period.</summary>
    Task<PerformanceActionResult> RunForSupplierAsync(string supplierId, RunReviewDto dto, string userId);
    /// <summary>Records that a sub-40 review has been escalated to the MD for a blacklist decision.</summary>
    Task<PerformanceActionResult> EscalateAsync(string id, EscalateReviewDto dto, string userId);
}
