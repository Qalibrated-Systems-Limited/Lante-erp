using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

public class FinanceServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FinanceServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IFinanceServiceClient
{
    protected override string ClientName => "FinanceService";
    protected override string ConfigKey => "FinanceService";

    public Task<ProfitLossDto?> GetPnlAsync(string periodId) =>
        GetAsync<ProfitLossDto>($"/api/v1/finance/month-end/{Uri.EscapeDataString(periodId)}/pnl");

    public Task<TrialBalanceDto?> GetTrialBalanceAsync(DateTime? asOf) =>
        GetAsync<TrialBalanceDto>($"/api/v1/finance/trial-balance{BuildQuery(new Dictionary<string, string?> { ["asOf"] = asOf?.ToString("o") })}");

    public Task<List<BudgetReadDto>?> GetBudgetsAsync(string fiscalYearId) =>
        GetAsync<List<BudgetReadDto>>($"/api/v1/finance/budgets{BuildQuery(new Dictionary<string, string?> { ["fiscalYearId"] = fiscalYearId })}");

    public Task<List<RevenueTargetReadDto>?> GetRevenueTargetsAsync(string fiscalYearId) =>
        GetAsync<List<RevenueTargetReadDto>>($"/api/v1/finance/revenue-targets{BuildQuery(new Dictionary<string, string?> { ["fiscalYearId"] = fiscalYearId })}");

    public Task<List<FixedAssetRowDto>?> GetFixedAssetsAsync() =>
        GetAsync<List<FixedAssetRowDto>>("/api/v1/finance/fixed-assets");

    public Task<List<DepreciationEntryRowDto>?> GetDepreciationScheduleAsync(string? period) =>
        GetAsync<List<DepreciationEntryRowDto>>(
            $"/api/v1/finance/fixed-assets/depreciation/schedule{BuildQuery(new Dictionary<string, string?> { ["period"] = period })}");

    public Task<List<AssetDisposalRowDto>?> GetAssetDisposalsAsync() =>
        GetAsync<List<AssetDisposalRowDto>>("/api/v1/finance/fixed-assets/disposals");

    public Task<List<DebtorAgingRowDto>?> GetAgedDebtorsAsync(DateTime? asOf) =>
        GetAsync<List<DebtorAgingRowDto>>($"/api/v1/finance/debtors/aging{BuildQuery(new Dictionary<string, string?> { ["asOf"] = asOf?.ToString("o") })}");

    public Task<CashFlowDto?> GetCashFlowForecastAsync(DateTime? asOf, int weeks) =>
        GetAsync<CashFlowDto>($"/api/v1/finance/cash-flow{BuildQuery(new Dictionary<string, string?> { ["asOf"] = asOf?.ToString("o"), ["weeks"] = weeks.ToString() })}");

    public Task<List<FiscalYearDto>?> GetFiscalYearsAsync() =>
        GetAsync<List<FiscalYearDto>>("/api/v1/finance/fiscal-years");
}
