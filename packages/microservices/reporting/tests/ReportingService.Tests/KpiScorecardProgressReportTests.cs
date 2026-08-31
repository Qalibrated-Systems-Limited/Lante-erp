using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingService.Core.Entities;
using ReportingService.Core.Enums;
using ReportingService.Core.Services;
using ReportingService.Infrastructure.Data;
using ReportingService.Infrastructure.Repositories;
using ReportingService.Infrastructure.Services;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// Report #10 — KPI Scorecard Progress (#225). The tenth of fifteen, and the only one needing no service
/// client: scorecards, data sources and the metric resolver all live inside reporting.
///
/// <para><b>What these cover and what they do not.</b> The measured-value path is exercised in the
/// FAILING direction only — an unknown metric key, and a deleted data source. Resolving a metric for real
/// dispatches a whole other report through the service provider, so a happy-path test here would need the
/// entire graph stood up and would be testing those reports rather than this one. The grading itself is
/// covered directly in <see cref="KpiScorecardStatusRulesTests"/>, against the shared rule this report
/// uses. That is the honest split, and the gap is the end-to-end resolve.</para>
///
/// <para>SQLite rather than InMemory, for consistency with the other reporting tests and because the
/// report reads through the real repository and CRUD service rather than a stub.</para>
/// </summary>
public class KpiScorecardProgressReportTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ReportingDbContext _db;
    private readonly KpiScorecardProgressReportService _sut;

    public KpiScorecardProgressReportTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ReportingDbContext(
            new DbContextOptionsBuilder<ReportingDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();

        _sut = new KpiScorecardProgressReportService(
            new ReportingCrudService<KpiScorecard>(new GenericRepository<KpiScorecard>(_db)),
            new ReportingCrudService<DataSource>(new GenericRepository<DataSource>(_db)),
            new MetricResolverService(),
            NullLogger<KpiScorecardProgressReportService>.Instance);
    }

    private DataSource Source(string id, string metricKey, string name = "Source") 
    {
        var d = new DataSource { Id = id, Name = name, MetricKey = metricKey, ModuleName = ReportCategory.Operations };
        _db.DataSources.Add(d);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return d;
    }

    /// <summary>
    /// Soft-deletes a data source — the ONLY way a scorecard can outlive the source it points at.
    ///
    /// <para>My first attempt seeded a scorecard with a dangling id and SQLite rejected it: the FK is
    /// <c>OnDelete(DeleteBehavior.Restrict)</c>, so a dangling id cannot be created and a source with
    /// scorecards cannot be hard-deleted. I briefly took that to mean the report's "data source not
    /// found" branch was dead code — the defect class this session has been full of. It is not. A
    /// soft-deleted source still satisfies the FK while disappearing from every query, because
    /// <c>DataSource</c> carries <c>HasQueryFilter(e => !e.IsDeleted)</c> — and soft delete is what the
    /// UI actually does. The branch is reachable by the likely route, not the impossible one.</para>
    /// </summary>
    private void SoftDelete(string dataSourceId)
    {
        var d = _db.DataSources.IgnoreQueryFilters().Single(x => x.Id == dataSourceId);
        d.IsDeleted = true;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private KpiScorecard Scorecard(string name, string dataSourceId, decimal target = 80m,
                                   decimal warn = 70m, decimal crit = 60m)
    {
        var s = new KpiScorecard
        {
            Name = name, DataSourceId = dataSourceId,
            TargetValue = target, WarningThreshold = warn, CriticalThreshold = crit,
            Period = ScorecardPeriod.Monthly,
        };
        _db.KpiScorecards.Add(s);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return s;
    }

    // ── The empty case ───────────────────────────────────────────────────────────

    [Fact]
    public async Task With_no_scorecards_the_report_is_empty_rather_than_null()
    {
        var report = await _sut.GetAsync(new EmptyProvider());

        // A dashboard that has to distinguish "no scorecards configured" from "the report failed" needs
        // this to be a real, empty report.
        report.Should().NotBeNull();
        report.Scorecards.Should().BeEmpty();
        report.Summary.Total.Should().Be(0);
        report.Warnings.Should().BeEmpty();
    }

    // ── A scorecard pointing at nothing ──────────────────────────────────────────

    [Fact]
    public async Task A_scorecard_whose_data_source_was_deleted_is_reported_not_dropped()
    {
        Source("ds-1", "not.a.real.metric");
        Scorecard("Orphaned", dataSourceId: "ds-1");
        SoftDelete("ds-1");

        var report = await _sut.GetAsync(new EmptyProvider());

        // Dropping it would make a scorecard someone relies on silently vanish from the dashboard, which
        // is worse than showing it broken.
        report.Scorecards.Should().ContainSingle();
        report.Scorecards[0].Error.Should().Contain("Data source not found");
        report.Scorecards[0].CurrentValue.Should().BeNull();
        report.Scorecards[0].Status.Should().BeNull();
        report.Warnings.Should().ContainSingle().Which.Should().Contain("Orphaned");
    }

    [Fact]
    public async Task An_unresolvable_metric_becomes_a_warning_not_a_failure()
    {
        Source("ds-1", metricKey: "not.a.real.metric");
        Scorecard("Broken metric", "ds-1");

        var report = await _sut.GetAsync(new EmptyProvider());

        // One unresolvable metric must not fail the whole report — the other scorecards are still worth
        // showing, and an upstream service being down is the common case rather than the exception.
        report.Scorecards.Should().ContainSingle();
        report.Scorecards[0].Error.Should().NotBeNullOrEmpty();
        report.Scorecards[0].Status.Should().BeNull();
        report.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task One_broken_scorecard_does_not_hide_the_others()
    {
        Source("ds-1", "not.a.real.metric");
        Source("ds-2", "also.not.real");
        Scorecard("Alpha", "ds-1");
        Scorecard("Beta", "ds-2");
        SoftDelete("ds-2");          // Beta's source is gone; Alpha's metric is unresolvable

        var report = await _sut.GetAsync(new EmptyProvider());

        report.Scorecards.Should().HaveCount(2);
        report.Warnings.Should().HaveCount(2);
    }

    // ── The summary ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unmeasured_scorecards_are_counted_separately_from_critical()
    {
        Source("ds-1", "not.a.real.metric");
        Scorecard("Cannot measure", "ds-1");

        var report = await _sut.GetAsync(new EmptyProvider());

        // "We do not know" and "it is bad" are different facts. Folding unmeasured into Critical either
        // raises a false alarm or hides a broken integration behind a real problem.
        report.Summary.Unmeasured.Should().Be(1);
        report.Summary.Critical.Should().Be(0);
        report.Summary.Total.Should().Be(1);
    }

    [Fact]
    public async Task The_summary_totals_agree_with_the_rows()
    {
        Source("ds-1", "not.a.real.metric");
        Source("ds-2", "also.not.real");
        Scorecard("A", "ds-1");
        Scorecard("B", "ds-1");
        Scorecard("C", "ds-2");
        SoftDelete("ds-2");

        var report = await _sut.GetAsync(new EmptyProvider());

        var s = report.Summary;
        (s.OnTarget + s.Warning + s.Critical + s.Unmeasured).Should().Be(s.Total);
        s.Total.Should().Be(report.Scorecards.Count);
    }

    // ── Presentation the caller should not have to re-derive ─────────────────────

    [Fact]
    public async Task Each_row_states_which_direction_is_good()
    {
        Source("ds-1", "not.a.real.metric");
        Scorecard("Higher better", "ds-1", target: 80m, warn: 70m, crit: 60m);
        Scorecard("Lower better", "ds-1", target: 0m, warn: 100m, crit: 500m);

        var report = await _sut.GetAsync(new EmptyProvider());

        // Carried on the row so a client can render an arrow without reimplementing the threshold
        // inference — which is exactly how the rule would end up duplicated a third time.
        report.Scorecards.Single(r => r.Name == "Higher better").HigherIsBetter.Should().BeTrue();
        report.Scorecards.Single(r => r.Name == "Lower better").HigherIsBetter.Should().BeFalse();
    }

    [Fact]
    public async Task Scorecards_come_back_in_name_order()
    {
        Source("ds-1", "not.a.real.metric");
        Scorecard("Zulu", "ds-1");
        Scorecard("Alpha", "ds-1");
        Scorecard("Mike", "ds-1");

        var report = await _sut.GetAsync(new EmptyProvider());

        // Stable ordering, so the dashboard does not reshuffle between refreshes on insertion order.
        report.Scorecards.Select(r => r.Name).Should().Equal("Alpha", "Mike", "Zulu");
    }

    [Fact]
    public async Task The_thresholds_are_carried_through_for_display()
    {
        Source("ds-1", "not.a.real.metric", name: "Fleet utilisation source");
        Scorecard("Utilisation", "ds-1", target: 85m, warn: 75m, crit: 65m);

        var report = await _sut.GetAsync(new EmptyProvider());

        var row = report.Scorecards.Single();
        row.TargetValue.Should().Be(85m);
        row.WarningThreshold.Should().Be(75m);
        row.CriticalThreshold.Should().Be(65m);
        row.DataSourceName.Should().Be("Fleet utilisation source");
        row.MetricKey.Should().Be("not.a.real.metric");
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    /// <summary>
    /// A service provider that resolves nothing. Sufficient because every path exercised here fails
    /// before dispatch — an unknown metric key is rejected by the resolver's own lookup, and a missing
    /// data source never reaches it.
    /// </summary>
    private sealed class EmptyProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
