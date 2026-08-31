using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Enums;
using ReportingService.Core.Interfaces;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Core.Services;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Report #10: kpi-scorecard-progress. Every configured scorecard against its target in one view (#225).
///
/// <para>Unlike the other nine reports this needs no service client — scorecards, data sources and the
/// metric resolver all live inside reporting. That is why it was the cheapest of the six remaining to
/// build: it proves the whole registration path end to end without new infrastructure.</para>
///
/// <para>Status and variance come from <see cref="KpiScorecardStatusRules"/>, shared with
/// <c>KpiScorecardsController</c>'s single-scorecard endpoint rather than reimplemented, so the report
/// and the detail view can never disagree about whether something is red.</para>
/// </summary>
public class KpiScorecardProgressReportService(
    IReportingCrudService<KpiScorecard> scorecards,
    IReportingCrudService<DataSource> dataSources,
    MetricResolverService resolver,
    ILogger<KpiScorecardProgressReportService> logger) : IKpiScorecardProgressReportService
{
    public async Task<KpiScorecardProgressReportDto> GetAsync(IServiceProvider sp)
    {
        var report = new KpiScorecardProgressReportDto();

        var all = (await scorecards.GetAllAsync()).OrderBy(s => s.Name).ToList();
        var sources = (await dataSources.GetAllAsync()).ToDictionary(d => d.Id);

        foreach (var s in all)
        {
            var row = new KpiScorecardProgressRowDto
            {
                ScorecardId = s.Id,
                Name = s.Name,
                Period = s.Period,
                TargetValue = s.TargetValue,
                WarningThreshold = s.WarningThreshold,
                CriticalThreshold = s.CriticalThreshold,
                HigherIsBetter = KpiScorecardStatusRules.HigherIsBetter(s),
            };

            if (!sources.TryGetValue(s.DataSourceId, out var source))
            {
                // A scorecard pointing at a deleted data source is a configuration fault, not a metric
                // failure. Reported as unmeasured with the reason rather than dropped: dropping it makes
                // a scorecard someone is relying on silently vanish from the dashboard.
                row.Error = "Data source not found.";
                report.Warnings.Add($"{s.Name}: data source {s.DataSourceId} no longer exists.");
                report.Scorecards.Add(row);
                continue;
            }

            row.DataSourceName = source.Name;
            row.MetricKey = source.MetricKey;

            try
            {
                var value = await resolver.ResolveAsync(sp, source.MetricKey);
                row.CurrentValue = value;
                row.Variance = KpiScorecardStatusRules.Variance(s, value);
                row.Status = KpiScorecardStatusRules.Resolve(s, value);
            }
            catch (Exception ex)
            {
                // One unresolvable metric must not fail the whole report — the other scorecards are still
                // worth showing, and an upstream service being down is the common case. Same posture as
                // ReportHelpers.SafeCallAsync in the client-backed reports.
                logger.LogWarning(ex, "Could not resolve metric {MetricKey} for scorecard {Scorecard}",
                    source.MetricKey, s.Name);
                row.Error = ex.Message;
                report.Warnings.Add($"{s.Name}: could not measure {source.MetricKey} — {ex.Message}");
            }

            report.Scorecards.Add(row);
        }

        report.Summary = new KpiScorecardProgressSummaryDto
        {
            Total = report.Scorecards.Count,
            OnTarget = report.Scorecards.Count(r => r.Status == ScorecardStatus.OnTarget),
            Warning = report.Scorecards.Count(r => r.Status == ScorecardStatus.Warning),
            Critical = report.Scorecards.Count(r => r.Status == ScorecardStatus.Critical),
            // Counted separately, never folded into Critical: "we could not measure this" and "this is
            // bad" are different facts. Conflating them either raises a false alarm or hides a broken
            // integration behind a real one.
            Unmeasured = report.Scorecards.Count(r => r.Status == null),
        };

        return report;
    }
}
