using HrService.Core.DTOs.Probation;

namespace HrService.Core.Interfaces.Services;

/// <summary>H2 (HR-003/HR-006, P3 + P33) — probation milestones and fixed-term contract renewal. The sweep
/// raises the records; HR and the line manager (or MD) then record the outcome.</summary>
public interface IProbationService
{
    Task<ProbationSummaryDto> GetSummaryAsync();

    // Probation (P3)
    Task<List<ProbationReviewDto>> ListReviewsAsync(string? outcome, string? employeeId);
    Task<ProbationActionResult> RecordOutcomeAsync(string reviewId, RecordProbationOutcomeDto dto, string userId, string? userName);

    // Contract renewal (P33)
    Task<List<ContractRenewalAlertDto>> ListContractAlertsAsync(string? outcome);
    Task<ProbationActionResult> RenewContractAsync(string alertId, RenewContractDto dto, string userId);
    Task<ProbationActionResult> ConvertToPermanentAsync(string alertId, ConvertToPermanentDto dto, string userId);
    Task<ProbationActionResult> LetContractExpireAsync(string alertId, LetContractExpireDto dto, string userId);

    /// <summary>The daily sweep: raises due probation reviews, opens contract alerts, fires the 30/7-day
    /// warnings and flags lapsed contracts. Idempotent — safe to run repeatedly and to trigger manually.
    /// <paramref name="tenantSchema"/> is only used to address alert delivery.</summary>
    Task<MilestoneSweepResultDto> RunMilestoneSweepAsync(string? tenantSchema, string userId);
}
