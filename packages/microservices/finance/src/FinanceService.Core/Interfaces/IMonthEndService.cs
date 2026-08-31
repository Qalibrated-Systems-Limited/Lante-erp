using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IMonthEndService
{
    /// Period close status + checklist (creates the default checklist on first access).
    Task<PeriodCloseDto> GetCloseAsync(string periodId);
    Task<PeriodCloseDto> ToggleItemAsync(string itemId, bool complete, string? actor);
    /// Closes + locks the period. Requires every checklist item complete (FIN-003/004).
    Task<PeriodCloseDto> CloseAsync(string periodId, string? actor);
    Task<PeriodCloseDto> ReopenAsync(string periodId, string? actor);
    /// P&L from the posted GL for a period (income − expense), with per-account + per-department detail.
    Task<ProfitLossDto> ProfitAndLossAsync(string periodId);
}
