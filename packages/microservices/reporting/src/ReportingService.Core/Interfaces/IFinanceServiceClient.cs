using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IFinanceServiceClient
{
    Task<ProfitLossDto?> GetPnlAsync(string periodId);
    Task<TrialBalanceDto?> GetTrialBalanceAsync(DateTime? asOf);
    Task<List<BudgetReadDto>?> GetBudgetsAsync(string fiscalYearId);
    Task<List<RevenueTargetReadDto>?> GetRevenueTargetsAsync(string fiscalYearId);
    Task<List<DebtorAgingRowDto>?> GetAgedDebtorsAsync(DateTime? asOf);
    Task<CashFlowDto?> GetCashFlowForecastAsync(DateTime? asOf, int weeks);
    Task<List<FiscalYearDto>?> GetFiscalYearsAsync();

    // Report #11 (#225). Three calls rather than one: finance exposes the register, the depreciation
    // schedule and disposals as separate endpoints, and combining them server-side there would mean a
    // finance endpoint that exists only for reporting.
    Task<List<FixedAssetRowDto>?> GetFixedAssetsAsync();
    Task<List<DepreciationEntryRowDto>?> GetDepreciationScheduleAsync(string? period);
    Task<List<AssetDisposalRowDto>?> GetAssetDisposalsAsync();
}
