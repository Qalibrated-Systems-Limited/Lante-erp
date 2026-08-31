using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Analytics;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>
/// PR4a — earned value, the S-curve and the portfolio rollup.
///
/// <para>All of it is derived; nothing new is stored. That is only possible because PR1 gave real
/// baselines, PR2 gave real actuals and PR3 made the baseline move only by agreement.</para>
///
/// <para><b>The three quantities, and where each comes from:</b>
/// <list type="bullet">
/// <item><b>PV</b> — the baseline's opinion of progress at a date. Each milestone earns its planned
/// amount linearly between <c>BaselineStart</c> and <c>BaselineDue</c>. Linear is a choice: without
/// resource loading (declined in PR1) there is no better within-milestone shape available.</item>
/// <item><b>EV</b> — milestone worth × its progress. Taken per milestone rather than
/// <c>BAC × overall%</c>, so a project whose milestone amounts do not add up to the approved budget
/// reports what its milestones are actually worth instead of silently rescaling them.</item>
/// <item><b>AC</b> — cost entries dated on or before the as-of date; falls back to the project's
/// running <c>ActualCost</c> when no dated entries exist.</item>
/// </list></para>
/// </summary>
public class ProjectAnalyticsService(
    IGenericRepository<Project> projects,
    IGenericRepository<Milestone> milestones,
    IGenericRepository<CostEntry> costEntries) : IProjectAnalyticsService
{
    /// <summary>Statuses that belong in a portfolio view — live work, not drafts or dead projects.</summary>
    private static readonly ProjectStatus[] PortfolioStatuses =
        [ProjectStatus.Active, ProjectStatus.OnHold, ProjectStatus.Completed];

    public async Task<ProjectEvmDto> GetProjectEvmAsync(string projectId, DateTime? asOf = null)
    {
        var project = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var at = (asOf ?? DateTime.UtcNow).Date;
        var ms = await milestones.Query()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .ToListAsync();
        var costs = await costEntries.Query()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted)
            .ToListAsync();

        var metrics = Compute(project, ms, costs, at);
        return new ProjectEvmDto { Metrics = metrics, SCurve = BuildSCurve(project, ms, costs, at) };
    }

    public async Task<PortfolioEvmDto> GetPortfolioEvmAsync(DateTime? asOf = null)
    {
        var at = (asOf ?? DateTime.UtcNow).Date;

        var list = await projects.Query()
            .Where(p => !p.IsDeleted && PortfolioStatuses.Contains(p.Status))
            .ToListAsync();
        if (list.Count == 0)
            return new PortfolioEvmDto { AsOf = at };

        var ids = list.Select(p => p.Id).ToList();
        var allMs = await milestones.Query().Where(m => ids.Contains(m.ProjectId) && !m.IsDeleted).ToListAsync();
        var allCosts = await costEntries.Query().Where(c => ids.Contains(c.ProjectId) && !c.IsDeleted).ToListAsync();

        var msByProject = allMs.GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.ToList());
        var costByProject = allCosts.GroupBy(c => c.ProjectId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = list
            .Select(p => Compute(p,
                msByProject.GetValueOrDefault(p.Id, []),
                costByProject.GetValueOrDefault(p.Id, []),
                at))
            .ToList();

        var bac = rows.Sum(r => r.Bac);
        var pv  = rows.Sum(r => r.Pv);
        var ev  = rows.Sum(r => r.Ev);
        var ac  = rows.Sum(r => r.Ac);

        // Portfolio indices are computed from the summed quantities, not averaged from the per-project
        // ratios — averaging would let a tiny project's wild ratio outweigh a large one's.
        decimal? spi = pv > 0 ? Round(ev / pv) : null;
        decimal? cpi = ac > 0 ? Round(ev / ac) : null;
        decimal? eac = cpi is > 0 ? Round(bac / cpi.Value) : null;

        return new PortfolioEvmDto
        {
            AsOf = at,
            ProjectCount = rows.Count,
            Bac = bac, Pv = pv, Ev = ev, Ac = ac,
            Spi = spi, Cpi = cpi,
            Eac = eac,
            Vac = eac is null ? null : (decimal?)(bac - eac.Value),
            OverrunCount = rows.Count(r => r.Vac is < 0),
            BehindCount  = rows.Count(r => r.Spi is < 1),
            // Worst forecast overrun first — the point of a portfolio view is what needs attention.
            Projects = rows.OrderBy(r => r.Vac ?? decimal.MaxValue).ToList(),
        };
    }

    // ── Computation ──────────────────────────────────────────────────────────────

    private static EvmMetricsDto Compute(Project project, List<Milestone> ms, List<CostEntry> costs, DateTime at)
    {
        // BaselineBudget is nullable — an unbaselined project has none, and falling back to the
        // planned budget is what lets EVM say something useful before approval (with a caveat).
        var baselined = project.BaselineSetAt != null && (project.BaselineBudget ?? 0m) > 0m;
        decimal bac = baselined ? project.BaselineBudget!.Value : project.PlannedBudget;

        var priced = ms.Where(m => (m.PlannedAmount ?? 0m) > 0m).ToList();

        var ev = priced.Sum(m => (m.PlannedAmount ?? 0m) * Clamp01(m.ProgressPct / 100m));
        var pv = priced.Sum(m => (m.PlannedAmount ?? 0m) * PlannedFraction(m, at));

        // Dated entries are the truthful source for "cost as at a date"; ActualCost is only a running
        // total with no history, so it can stand in for today but never for a past as-of date.
        var ac = costs.Count > 0
            ? costs.Where(c => c.EntryDate.Date <= at).Sum(c => c.Amount)
            : project.ActualCost;

        decimal? spi = pv > 0m ? Round(ev / pv) : null;
        decimal? cpi = ac > 0m ? Round(ev / ac) : null;
        decimal? eac = cpi is > 0m ? Round(bac / cpi.Value) : null;

        var caveats = new List<string>();
        if (!baselined)
            caveats.Add("No approved baseline — measured against the current planned budget, which can move without a change request.");
        if (priced.Count == 0)
            caveats.Add("No milestone carries a planned amount, so there is nothing to earn value against.");
        else if (priced.Count < ms.Count)
            caveats.Add($"{ms.Count - priced.Count} of {ms.Count} milestones have no planned amount and are excluded.");
        var pricedTotal = priced.Sum(m => m.PlannedAmount ?? 0m);
        if (pricedTotal > 0m && bac > 0m && Math.Abs(pricedTotal - bac) / bac > 0.01m)
            caveats.Add($"Milestone amounts total {pricedTotal:N0} against a budget of {bac:N0} — earned value is measured on the milestones.");

        return new EvmMetricsDto
        {
            ProjectId = project.Id, ProjectName = project.Name, Status = project.Status.ToString(),
            AsOf = at,
            Bac = bac, IsBaselined = baselined,
            Pv = Round(pv), Ev = Round(ev), Ac = Round(ac),
            ScheduleVariance = Round(ev - pv),
            CostVariance     = Round(ev - ac),
            Spi = spi, Cpi = cpi,
            Eac = eac,
            Etc = eac is null ? null : (decimal?)Round(eac.Value - ac),
            Vac = eac is null ? null : (decimal?)Round(bac - eac.Value),
            PercentComplete = pricedTotal > 0m ? (int)Math.Round(ev / pricedTotal * 100m) : 0,
            PercentSpent    = bac > 0m ? (int)Math.Round(ac / bac * 100m) : 0,
            ScheduleVerdict = ScheduleVerdictOf(spi, ev, pv),
            CostVerdict     = CostVerdictOf(cpi, ev, ac),
            Caveats = caveats,
        };
    }

    /// <summary>
    /// How much of a milestone the baseline expected to be done by <paramref name="at"/>. Uses the
    /// baseline dates, never the live ones — measuring against a plan that moves with the work would
    /// report every project as perfectly on schedule.
    /// </summary>
    private static decimal PlannedFraction(Milestone m, DateTime at)
    {
        var due = m.BaselineDue?.Date;
        if (due is null) return 0m;                      // never baselined — contributes no planned value
        if (at >= due.Value) return 1m;

        var start = m.BaselineStart?.Date;
        // No baseline start means the milestone is a point in time, not a bar: nothing is expected
        // until its due date arrives.
        if (start is null || start >= due) return 0m;
        if (at <= start.Value) return 0m;

        var span = (decimal)(due.Value - start.Value).TotalDays;
        return span <= 0m ? 0m : Clamp01((decimal)(at - start.Value).TotalDays / span);
    }

    /// <summary>
    /// Weekly cumulative curve. PV runs the full baseline span; EV and AC stop at today.
    ///
    /// <para><b>Historical EV is milestone-completion-based</b> — a milestone contributes its full
    /// amount from the date it was signed off. Progress percentages are not versioned, so partial
    /// progress has no history to plot; the final point uses live progress so today's figure matches
    /// the tiles. Reading the curve as "what had we definitively earned by then" is the honest
    /// interpretation.</para>
    /// </summary>
    private static List<SCurvePointDto> BuildSCurve(Project project, List<Milestone> ms, List<CostEntry> costs, DateTime at)
    {
        var priced = ms.Where(m => (m.PlannedAmount ?? 0m) > 0m).ToList();
        if (priced.Count == 0) return [];

        var starts = priced.Select(m => m.BaselineStart?.Date).Where(d => d is not null).Select(d => d!.Value).ToList();
        var ends   = priced.Select(m => m.BaselineDue?.Date).Where(d => d is not null).Select(d => d!.Value).ToList();
        if (ends.Count == 0) return [];

        var first = starts.Count > 0 ? starts.Min() : ends.Min();
        if (project.StartDate != default && project.StartDate.Date < first) first = project.StartDate.Date;
        var last = ends.Max();
        // The as-of date has to sit inside the span at both ends. A baseline pushed into the future by
        // an approved delay leaves the whole curve later than today — and without this the earned and
        // actual series would have no points at all to draw, despite both having real values now.
        if (at > last) last = at;
        if (at < first) first = at;

        // Cap the point count so a multi-year project does not return thousands of rows; the step
        // widens instead of the range shrinking, so the curve keeps its full span.
        var totalDays = (last - first).TotalDays;
        if (totalDays <= 0) return [];
        var stepDays = Math.Max(7, (int)Math.Ceiling(totalDays / 120));

        var points = new List<SCurvePointDto>();
        for (var d = first; d <= last; d = d.AddDays(stepDays))
            points.Add(PointAt(d));
        if (points.Count == 0 || points[^1].Date < last) points.Add(PointAt(last));

        // Today rarely lands on a step, and it is the one date that must be plotted: it carries the
        // live partial progress and the full actual cost, which is where the reader looks first.
        if (at >= first && at <= last && points.All(p => p.Date != at))
        {
            points.Add(PointAt(at));
            points = points.OrderBy(p => p.Date).ToList();
        }

        return points;

        SCurvePointDto PointAt(DateTime d)
        {
            var isActual = d <= at;
            var pv = priced.Sum(m => (m.PlannedAmount ?? 0m) * PlannedFraction(m, d));

            decimal ev;
            if (!isActual) ev = 0m;
            else if (d >= at)
                ev = priced.Sum(m => (m.PlannedAmount ?? 0m) * Clamp01(m.ProgressPct / 100m));
            else
                ev = priced.Where(m => m.SignOffAt.HasValue && m.SignOffAt.Value.Date <= d)
                           .Sum(m => m.PlannedAmount ?? 0m);

            var ac = !isActual ? 0m
                : costs.Count > 0 ? costs.Where(c => c.EntryDate.Date <= d).Sum(c => c.Amount)
                : (d >= at ? project.ActualCost : 0m);

            return new SCurvePointDto
            {
                Date = d, Pv = Round(pv), Ev = Round(ev), Ac = Round(ac), IsActual = isActual,
            };
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static decimal Clamp01(decimal v) => v < 0m ? 0m : v > 1m ? 1m : v;
    private static decimal Round(decimal v) => decimal.Round(v, 2);

    /// <summary>
    /// A zero denominator is not missing data, it is a fact about the plan, and saying so beats a
    /// blank index. PV can legitimately be zero when the baseline says nothing was due to start yet —
    /// exactly the state a project is left in after an approved delay pushes the baseline forward.
    /// </summary>
    private static string ScheduleVerdictOf(decimal? spi, decimal ev, decimal pv) => pv > 0m
        ? Verdict(spi, "ahead of schedule", "behind schedule")
        : ev > 0m ? "ahead of baseline — no work was due to have started yet" : "not started";

    private static string CostVerdictOf(decimal? cpi, decimal ev, decimal ac) => ac > 0m
        ? Verdict(cpi, "under budget", "over budget")
        : ev > 0m ? "earning value with no cost booked yet" : "nothing spent";

    private static string Verdict(decimal? index, string good, string bad) => index switch
    {
        null => "not enough data yet",
        // A percent or so either side of 1.0 is noise, not a signal worth acting on.
        >= 0.99m and <= 1.01m => "on plan",
        > 1.01m => $"{good} ({index:0.00})",
        _ => $"{bad} ({index:0.00})",
    };
}
