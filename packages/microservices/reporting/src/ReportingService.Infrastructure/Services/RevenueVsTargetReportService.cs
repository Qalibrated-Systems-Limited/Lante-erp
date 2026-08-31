using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Report #14: revenue-vs-target. Revenue against target, by department (#225).
///
/// <para>Finance's revenue targets carry the department in <c>Scope</c>, and finance computes the
/// actual per target from posted income in the GL. This groups them, separates company-wide from
/// departmental, and rolls the departments up.</para>
///
/// <para><b>The aggregation trap.</b> <c>ListTargetsAsync</c> filters on fiscal year alone, and
/// <c>RevenueTarget</c> has no version or active flag, so a department can legitimately end up with
/// several target rows. Each row's <c>Actual</c> is computed from its own cost centre — which the read
/// DTO does not expose — so two rows for one department may be reporting the SAME revenue twice or two
/// genuinely different cost centres, and nothing in the payload distinguishes them. Summing would
/// silently double the revenue in the first case. This report therefore sums targets, takes the actual
/// ONCE, and warns, rather than inventing a number it cannot justify. Same root cause as #347 on the
/// budget side.</para>
/// </summary>
public class RevenueVsTargetReportService(
    IFinanceServiceClient finance,
    ILogger<RevenueVsTargetReportService> logger) : IRevenueVsTargetReportService
{
    /// <summary>Finance's own label for the target that spans the whole business.</summary>
    private const string CompanyWideScope = "Company-wide";

    public async Task<RevenueVsTargetReportDto> GetAsync(string? fiscalYearId)
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(fiscalYearId))
        {
            fiscalYearId = await ResolveCurrentFiscalYearIdAsync(warnings);
            if (fiscalYearId == null)
            {
                var empty = new RevenueVsTargetReportDto { FiscalYearId = string.Empty };
                empty.Warnings.AddRange(warnings);
                return empty;
            }
        }

        var report = new RevenueVsTargetReportDto { FiscalYearId = fiscalYearId };

        var (targets, warning) = await ReportHelpers.SafeCallAsync(
            () => finance.GetRevenueTargetsAsync(fiscalYearId), "revenue targets", logger);
        if (warning != null) warnings.Add(warning);

        var rows = (targets ?? new List<RevenueTargetReadDto>())
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Scope) ? "(unscoped)" : t.Scope.Trim())
            .Select(g => Row(g.Key, g.ToList(), warnings))
            .OrderByDescending(r => r.Target)
            .ToList();

        report.CompanyWide = rows.FirstOrDefault(
            r => string.Equals(r.Department, CompanyWideScope, StringComparison.OrdinalIgnoreCase));

        // Departments exclude company-wide, so the roll-up never adds a total to the parts that
        // already make it up.
        report.Departments = rows.Where(r => !ReferenceEquals(r, report.CompanyWide)).ToList();

        report.Totals = new RevenueVsTargetTotalsDto
        {
            DepartmentCount = report.Departments.Count,
            TotalDepartmentTarget = report.Departments.Sum(r => r.Target),
            TotalDepartmentActual = report.Departments.Sum(r => r.Actual),
            TotalVariance = report.Departments.Sum(r => r.Variance),
            DepartmentsBehind = report.Departments.Count(r => r.Actual < r.Target),
        };

        report.Warnings.AddRange(warnings);
        return report;
    }

    private static DepartmentRevenueRowDto Row(string department, List<RevenueTargetReadDto> group, List<string> warnings)
    {
        var target = group.Sum(t => t.AnnualAmount);

        // Taken ONCE, not summed. See the aggregation note on the class: the read DTO does not carry
        // the cost centre, so duplicate rows for one department are indistinguishable from two real
        // cost centres, and summing would double revenue that was only earned once. The larger figure
        // is used because a target row covering a wider cost centre reports the wider actual.
        var actual = group.Max(t => t.Actual);

        if (group.Count > 1)
            warnings.Add(
                $"{department} has {group.Count} revenue targets for this fiscal year. Their targets are "
                + "summed, but the actual is taken once rather than added — the payload does not say "
                + "whether they cover the same cost centre, and adding would double-count revenue. See #347.");

        return new DepartmentRevenueRowDto
        {
            Department = department,
            Target = target,
            Actual = actual,
            Variance = actual - target,
            // Null rather than 0 when nothing was targeted: 0% reads as "achieved nothing" when the
            // truth is "nothing was asked for".
            AchievedPct = target == 0m ? null : Math.Round(actual / target * 100m, 2),
            // Finance's own grading, where a single row carries it. With several rows the per-row
            // status describes a different figure than the one shown, so it is recomputed.
            Status = group.Count == 1 && !string.IsNullOrWhiteSpace(group[0].Status)
                ? group[0].Status
                : Grade(actual, target),
            TargetRowCount = group.Count,
        };
    }

    /// <summary>Matches finance's own Behind / OnTrack / Achieved vocabulary.</summary>
    private static string Grade(decimal actual, decimal target) =>
        target <= 0m ? "OnTrack"
        : actual >= target ? "Achieved"
        : actual >= target * 0.9m ? "OnTrack"
        : "Behind";

    private async Task<string?> ResolveCurrentFiscalYearIdAsync(List<string> warnings)
    {
        var (years, warning) = await ReportHelpers.SafeCallAsync(
            () => finance.GetFiscalYearsAsync(), "fiscal years", logger);
        if (warning != null) warnings.Add(warning);
        if (years == null || years.Count == 0)
        {
            warnings.Add("No fiscal years configured — unable to resolve a default fiscal year.");
            return null;
        }

        var today = DateTime.UtcNow;
        return (years.FirstOrDefault(y => y.StartDate <= today && today <= y.EndDate) ?? years.First()).Id;
    }
}
