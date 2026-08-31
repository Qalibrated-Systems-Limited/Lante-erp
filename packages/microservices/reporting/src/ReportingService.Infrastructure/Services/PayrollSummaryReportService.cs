using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Report #12: payroll-summary. What payroll cost, per run and in total (#225).
///
/// <para>Defaults to APPROVED runs only. A draft or computed run's figures are not yet what anyone was
/// paid, and folding them into a cost report would report a number that was never spent. The caller can
/// ask for another status explicitly.</para>
/// </summary>
public class PayrollSummaryReportService(
    IHrServiceClient hr,
    ILogger<PayrollSummaryReportService> logger) : IPayrollSummaryReportService
{
    public async Task<PayrollSummaryReportDto> GetAsync(string? status)
    {
        var report = new PayrollSummaryReportDto();

        var (runs, warning) = await ReportHelpers.SafeCallAsync(
            () => hr.GetPayrollRunsAsync(status ?? "Approved"), "payroll runs", logger);
        if (warning != null) report.Warnings.Add(warning);

        report.Runs = (runs ?? new List<PayrollRunRowDto>())
            .OrderByDescending(r => r.PeriodStart)
            .ToList();

        report.Totals = new PayrollSummaryTotalsDto
        {
            RunCount = report.Runs.Count,

            // The MOST RECENT run's headcount, not a sum. Summing EmployeeCount across periods counts the
            // same person once per month and yields a number nobody can act on.
            LatestHeadcount = report.Runs.FirstOrDefault()?.EmployeeCount ?? 0,

            TotalGross = report.Runs.Sum(r => r.TotalGross),
            TotalPaye = report.Runs.Sum(r => r.TotalPaye),
            TotalStatutory = report.Runs.Sum(r => r.TotalStatutory),
            TotalOtherDeductions = report.Runs.Sum(r => r.TotalOtherDeductions),
            TotalNet = report.Runs.Sum(r => r.TotalNet),
            TotalEmployerCost = report.Runs.Sum(r => r.TotalEmployerCost),

            // Gross plus the employer's own contributions. This is what people mean by "what does payroll
            // cost", and gross alone understates it by the employer's NSSF and housing levy.
            TotalCostOfEmployment = report.Runs.Sum(r => r.TotalGross + r.TotalEmployerCost),

            // An approved run whose journal never posted is money owed to staff with no entry in the
            // accounts. Finance can retry it, but nothing surfaces the backlog — so this does.
            RunsWithUnpostedJournal = report.Runs.Count(r => r.ApprovedAt != null && r.JournalPostedAt == null),
        };

        return report;
    }
}
