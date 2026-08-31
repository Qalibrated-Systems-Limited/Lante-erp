using ProcurementService.Core.DTOs.Matching;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P6 (DEC-4) — the 3-way matching engine, the final gate before payment. For an issued LPO it
/// compares the order (procurement), the goods received (Stores GRN, pushed in via the P5 callback) and the
/// supplier invoice (read from Finance). A clean match hands a validated payment voucher to Finance;
/// any failed check raises a <c>MatchingException</c> for Finance-Manager resolution and blocks the voucher.</summary>
public interface IThreeWayMatchService
{
    Task<MatchListResult> GetAllAsync(MatchFilterParams filter);
    Task<MatchReadDto?> GetByIdAsync(string id);
    Task<MatchReadDto?> GetByPoAsync(string poId);
    Task<MatchSummaryDto> GetSummaryAsync();
    Task<List<MatchExceptionDto>> GetExceptionsAsync(string? status);

    /// <summary>Runs (or re-runs) the match for one LPO, refreshing the checks and reconciling exceptions.</summary>
    Task<MatchActionResult> RunAsync(string poId, string userId);

    /// <summary>Sweeps every issued LPO that is not yet cleanly matched through the engine.</summary>
    Task<MatchActionResult> RunPendingAsync(string userId);

    /// <summary>Finance-Manager resolution of a discrepancy; clearing the last open exception releases the
    /// match for payment (an explicit override, recorded in the audit trail).</summary>
    Task<MatchActionResult> ResolveExceptionAsync(string exceptionId, ResolveExceptionDto dto, string userId);

    /// <summary>Hands the validated voucher to Finance for disbursement.</summary>
    Task<MatchActionResult> RaiseVoucherAsync(string matchId, RaiseVoucherDto dto, string userId);
}
