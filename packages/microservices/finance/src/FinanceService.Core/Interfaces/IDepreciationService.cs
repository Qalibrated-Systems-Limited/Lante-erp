using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IDepreciationService
{
    /// Idempotent — re-running an already-run period is a no-op. Posts one consolidated
    /// journal entry (SourceModule="FixedAssets", SourceDocumentId=period) via IJournalService.
    Task<RunDepreciationResultDto> RunDepreciationAsync(string period, string? actorUserId);
    Task<List<DepreciationEntryReadDto>> GetScheduleAsync(string? period = null);
}
