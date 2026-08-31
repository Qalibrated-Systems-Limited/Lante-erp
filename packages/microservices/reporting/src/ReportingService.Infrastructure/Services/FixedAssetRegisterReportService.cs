using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Report #11: fixed-asset-register. The register, the depreciation charged, and disposals (#225).
///
/// <para>Three finance calls in parallel rather than one combined endpoint, because combining them
/// server-side would mean a finance endpoint existing only for reporting. Each goes through
/// <c>ReportHelpers.SafeCallAsync</c>, so a partial answer is returned with a warning rather than the
/// whole report failing — the same posture as the other client-backed reports.</para>
/// </summary>
public class FixedAssetRegisterReportService(
    IFinanceServiceClient finance,
    ILogger<FixedAssetRegisterReportService> logger) : IFixedAssetRegisterReportService
{
    public async Task<FixedAssetRegisterReportDto> GetAsync(string? period)
    {
        var report = new FixedAssetRegisterReportDto();

        var assetsTask = ReportHelpers.SafeCallAsync(() => finance.GetFixedAssetsAsync(), "fixed asset register", logger);
        var scheduleTask = ReportHelpers.SafeCallAsync(() => finance.GetDepreciationScheduleAsync(period), "depreciation schedule", logger);
        var disposalsTask = ReportHelpers.SafeCallAsync(() => finance.GetAssetDisposalsAsync(), "asset disposals", logger);

        await Task.WhenAll(assetsTask, scheduleTask, disposalsTask);

        var (assets, assetsWarning) = assetsTask.Result;
        var (schedule, scheduleWarning) = scheduleTask.Result;
        var (disposals, disposalsWarning) = disposalsTask.Result;

        report.Assets = assets ?? new List<FixedAssetRowDto>();
        report.DepreciationSchedule = schedule ?? new List<DepreciationEntryRowDto>();
        report.Disposals = disposals ?? new List<AssetDisposalRowDto>();

        foreach (var warning in new[] { assetsWarning, scheduleWarning, disposalsWarning })
            if (warning != null) report.Warnings.Add(warning);

        report.ByCategory = report.Assets
            .GroupBy(a => new { a.CategoryId, a.CategoryName })
            .Select(g => new FixedAssetCategoryTotalDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                AssetCount = g.Count(),
                AcquisitionCost = g.Sum(a => a.AcquisitionCost),
                AccumulatedDepreciation = g.Sum(a => a.AccumulatedDepreciation),
                NetBookValue = g.Sum(a => a.NetBookValue),
            })
            .OrderBy(c => c.CategoryName)
            .ToList();

        report.Summary = new FixedAssetRegisterSummaryDto
        {
            Period = period,
            AssetCount = report.Assets.Count,
            TotalAcquisitionCost = report.Assets.Sum(a => a.AcquisitionCost),
            TotalAccumulatedDepreciation = report.Assets.Sum(a => a.AccumulatedDepreciation),
            TotalNetBookValue = report.Assets.Sum(a => a.NetBookValue),

            // The charge for the period the schedule covers, NOT the accumulated figure above. The two
            // get confused constantly and differ by orders of magnitude on an established register, so
            // they are named differently and computed from different sources.
            DepreciationChargedInPeriod = report.DepreciationSchedule.Sum(d => d.Amount),

            DisposalCount = report.Disposals.Count,
            DisposalProceeds = report.Disposals.Sum(d => d.Proceeds ?? 0m),

            // Signed: positive is a gain. Proceeds are nullable — a donated or scrapped asset has none —
            // and a null there is zero proceeds against a real closing NBV, which is a loss, not a gap.
            DisposalGainOrLoss = report.Disposals.Sum(d => (d.Proceeds ?? 0m) - d.ClosingNbv),

            // An asset pending disposal approval is still on the books and its NBV is still counted in
            // the totals above, so the count belongs on the register rather than only in finance.
            DisposalsPendingApproval = report.Disposals.Count(d =>
                d.MdApprovedBy == null || (d.RequiresBoardApproval && d.BoardApprovedBy == null)),
        };

        return report;
    }
}
