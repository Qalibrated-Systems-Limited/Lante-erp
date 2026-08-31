using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #3: aged-debtors. Single upstream source — pure pass-through of FinanceService's
// GET /api/v1/finance/debtors/aging. Any failure propagates to the controller's try/catch, which
// maps it to a 502-style response (there's no partial data to salvage from a single source).
public class AgedDebtorsReportService(IFinanceServiceClient finance) : IAgedDebtorsReportService
{
    public async Task<List<DebtorAgingRowDto>> GetAsync(DateTime? asOf) =>
        await finance.GetAgedDebtorsAsync(asOf) ?? new List<DebtorAgingRowDto>();
}
