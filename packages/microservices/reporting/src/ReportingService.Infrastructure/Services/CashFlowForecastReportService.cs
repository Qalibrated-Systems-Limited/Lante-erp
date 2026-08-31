using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #4: cash-flow-forecast. Single upstream source — pure pass-through of FinanceService's
// GET /api/v1/finance/cash-flow. Failures propagate to the controller's try/catch.
public class CashFlowForecastReportService(IFinanceServiceClient finance) : ICashFlowForecastReportService
{
    public async Task<CashFlowDto> GetAsync(DateTime? asOf, int weeks) =>
        await finance.GetCashFlowForecastAsync(asOf, weeks) ?? new CashFlowDto { AsOf = asOf ?? DateTime.UtcNow };
}
