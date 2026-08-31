using ReportingService.Core.DTOs;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Resolves a DataSource.MetricKey to its current decimal value, for widgets/scorecards/red-flag
/// rules. Deliberately a typed dictionary of small extractor functions, not generic reflection —
/// the 9 report DTOs (ManagementAccountsReportDto, BudgetVarianceReportDto, ...) are too
/// differently shaped (nested, list-based, optional) for a dot-path reflection scheme to stay
/// robust. Each extractor's report key matches ReportGenerationService.DispatchAsync's switch.
/// </summary>
public class MetricResolverService
{
    // Declaration order matters — static field initializers run top-to-bottom, so Extractors
    // must be fully initialized before KnownMetricKeys reads from it.
    private static readonly Dictionary<string, (string ReportKey, Func<object, decimal> Extract)> Extractors = new()
    {
        // DispatchAsync calls GetAsync(null, null) — periodId null means ProfitAndLoss is always
        // null (ManagementAccountsReportService skips it without an explicit period). BalanceSheet
        // is derived from the trial balance instead, which has no such requirement, and its
        // CurrentYearProfit is the same incomeNet-minus-expenseNet figure as P&L net profit.
        ["management-accounts.net-profit"] = ("management-accounts", o => ((ManagementAccountsReportDto)o).BalanceSheet?.CurrentYearProfit ?? 0m),
        ["budget-variance.total-variance"] = ("budget-variance", o => ((BudgetVarianceReportDto)o).Budgets.Sum(b => b.Variance)),
        // The departmental roll-up, company-wide excluded — a scorecard tracking both would measure
        // the same revenue twice.
        ["revenue-vs-target.department-variance"] = ("revenue-vs-target", o => ((RevenueVsTargetReportDto)o).Totals.TotalVariance),
        ["aged-debtors.total-overdue"] = ("aged-debtors", o => ((List<DebtorAgingRowDto>)o).Sum(r => r.Days1To30 + r.Days31To60 + r.Days61Plus)),
        ["cash-flow-forecast.lowest-projected-balance"] = ("cash-flow-forecast", o => ((CashFlowDto)o).LowestProjectedBalance),
        ["project-profitability.utilization-percent"] = ("project-profitability", o => ((ProjectProfitabilityReportDto)o).Totals.UtilizationPercent),
        ["fleet-cost-utilisation.total-profit"] = ("fleet-cost-utilisation", o => ((FleetCostUtilisationReportDto)o).Totals.TotalProfit),
        ["procurement-spend.grand-total"] = ("procurement-spend", o => ((ProcurementSpendReportDto)o).GrandTotal),
        ["hse-incidents-trir.trir"] = ("hse-incidents-trir", o => ((HseIncidentsTrirReportDto)o).Dashboard?.Trir ?? 0m),
        ["fixed-asset-register.net-book-value"] = ("fixed-asset-register",
            o => ((FixedAssetRegisterReportDto)o).Summary.TotalNetBookValue),
        ["payroll-summary.cost-of-employment"] = ("payroll-summary",
            o => ((PayrollSummaryReportDto)o).Totals.TotalCostOfEmployment),
        ["leave-balance.days-remaining"] = ("leave-balance",
            o => ((LeaveBalanceReportDto)o).Totals.DaysRemaining),
        ["kpi-scorecard-progress.off-target-count"] = ("kpi-scorecard-progress",
            o => ((KpiScorecardProgressReportDto)o).Summary.Warning + ((KpiScorecardProgressReportDto)o).Summary.Critical),
        ["compliance-dashboard.licences-expired"] = ("compliance-dashboard", o => (decimal)(((ComplianceDashboardReportDto)o).ComplianceDashboard?.LicencesExpired ?? 0)),
    };

    public static readonly IReadOnlyList<string> KnownMetricKeys = Extractors.Keys.ToList();

    public async Task<decimal> ResolveAsync(IServiceProvider sp, string metricKey)
    {
        if (!Extractors.TryGetValue(metricKey, out var entry))
            throw new InvalidOperationException($"Unknown metric key '{metricKey}'.");

        var report = await ReportGenerationService.DispatchAsync(sp, entry.ReportKey);
        return entry.Extract(report);
    }
}
