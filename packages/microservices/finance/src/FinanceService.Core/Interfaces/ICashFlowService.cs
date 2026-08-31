using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface ICashFlowService
{
    /// Weekly cash-position forecast: cash-now (GL bank+cash) rolled forward by open receivables
    /// (by due week) less open payables (by due week). Overdue items are reported separately.
    Task<CashFlowDto> ForecastAsync(DateTime asOf, int weeks = 8);
}
