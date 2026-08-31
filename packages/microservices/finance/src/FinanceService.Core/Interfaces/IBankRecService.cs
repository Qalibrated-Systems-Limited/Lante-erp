using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IBankRecService
{
    /// Create a reconciliation, snapshot the GL bank balance as of the statement date, import the
    /// statement lines, and auto-match them to unmatched GL entries by signed amount.
    Task<ReconciliationReadDto> CreateAsync(CreateReconciliationDto dto, string? actor);
    Task<ReconciliationReadDto> GetAsync(string id);
    Task<List<ReconciliationSummaryDto>> ListAsync(int limit = 200);
    Task<ReconciliationReadDto> MatchAsync(string id, string lineId, string glEntryId, string? actor);
    Task<ReconciliationReadDto> UnmatchAsync(string id, string lineId, string? actor);
    /// Post an unmatched (bank-only) statement line as a journal against the given contra account.
    Task<ReconciliationReadDto> PostBankItemAsync(string id, string lineId, PostBankItemDto dto, string? actor);
    /// Finalise: only allowed when the difference is zero.
    Task<ReconciliationReadDto> CompleteAsync(string id, string? actor);
}
