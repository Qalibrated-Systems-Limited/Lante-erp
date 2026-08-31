using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #2: budget-variance. Both upstream endpoints already compute Actual/Variance/Status
// server-side in FinanceService — this is mostly a combine/pass-through, plus resolving "current
// fiscal year" server-side when the caller omits fiscalYearId (the frontend's "leave blank for
// current year" UI depends on this — it never sends the param at all when left blank).
public class BudgetVarianceReportService(
    IFinanceServiceClient finance,
    ILogger<BudgetVarianceReportService> logger) : IBudgetVarianceReportService
{
    public async Task<BudgetVarianceReportDto> GetAsync(string? fiscalYearId)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(fiscalYearId))
        {
            fiscalYearId = await ResolveCurrentFiscalYearIdAsync(warnings);
            if (fiscalYearId == null)
            {
                var empty = new BudgetVarianceReportDto { FiscalYearId = string.Empty };
                empty.Warnings.AddRange(warnings);
                return empty;
            }
        }

        var report = new BudgetVarianceReportDto { FiscalYearId = fiscalYearId };

        var budgetsTask = ReportHelpers.SafeCallAsync(() => finance.GetBudgetsAsync(fiscalYearId), "budgets", logger);
        var targetsTask = ReportHelpers.SafeCallAsync(() => finance.GetRevenueTargetsAsync(fiscalYearId), "revenue targets", logger);

        await Task.WhenAll(budgetsTask, targetsTask);

        var (budgets, budgetsWarning) = budgetsTask.Result;
        var (targets, targetsWarning) = targetsTask.Result;

        report.Budgets = budgets ?? new List<BudgetReadDto>();
        report.RevenueTargets = targets ?? new List<RevenueTargetReadDto>();

        report.Warnings.AddRange(warnings);
        if (budgetsWarning != null) report.Warnings.Add(budgetsWarning);
        if (targetsWarning != null) report.Warnings.Add(targetsWarning);

        return report;
    }

    // "Current" = the fiscal year whose date range contains today; falls back to the most recent
    // one (FiscalYears are already ordered by StartDate descending) if none contains today, e.g.
    // for a fiscal calendar that hasn't been rolled forward yet.
    private async Task<string?> ResolveCurrentFiscalYearIdAsync(List<string> warnings)
    {
        var (years, warning) = await ReportHelpers.SafeCallAsync(() => finance.GetFiscalYearsAsync(), "fiscal years", logger);
        if (warning != null) warnings.Add(warning);
        if (years == null || years.Count == 0)
        {
            warnings.Add("No fiscal years configured — unable to resolve a default fiscal year.");
            return null;
        }

        var today = DateTime.UtcNow;
        var current = years.FirstOrDefault(y => y.StartDate <= today && today <= y.EndDate) ?? years.First();
        return current.Id;
    }
}
