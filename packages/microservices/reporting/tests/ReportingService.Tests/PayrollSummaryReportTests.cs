using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;
using ReportingService.Infrastructure.Services;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// Report #12 — Payroll Summary &amp; Cost (#225).
///
/// <para>No database and no HTTP: this report is pure aggregation over what HrService returns, so the
/// seam under test is <see cref="IHrServiceClient"/> and a stub is the honest double. What that does NOT
/// cover is <c>HrServiceClient</c> itself — whether the two routes exist on HrService and whether the
/// mirrored <see cref="PayrollRunRowDto"/> deserialises from what HrService actually emits. That is
/// integration surface, shared with the seven service clients already in this service, and it is not
/// covered here for any of them.</para>
///
/// <para>The arithmetic below is worth asserting rather than eyeballing because three of the figures are
/// deliberately NOT the obvious sum: headcount is the latest run's, cost of employment adds a column that
/// is not part of gross, and the unposted-journal count is a two-condition filter.</para>
/// </summary>
public class PayrollSummaryReportTests
{
    private sealed class StubHr : IHrServiceClient
    {
        public List<PayrollRunRowDto>? Runs { get; set; } = new();
        public Exception? Throw { get; set; }

        /// <summary>What the service asked for — the default-status assertion reads this.</summary>
        public string? StatusAsked { get; private set; }
        public bool WasCalled { get; private set; }

        public Task<List<PayrollRunRowDto>?> GetPayrollRunsAsync(string? status)
        {
            WasCalled = true;
            StatusAsked = status;
            if (Throw != null) throw Throw;
            return Task.FromResult(Runs);
        }

        public Task<List<LeaveEntitlementRowDto>?> GetLeaveEntitlementsAsync(int? year) =>
            throw new NotSupportedException("This report does not read leave.");
    }

    private static PayrollSummaryReportService Sut(StubHr hr) =>
        new(hr, NullLogger<PayrollSummaryReportService>.Instance);

    private static PayrollRunRowDto Run(
        string id,
        DateTime periodStart,
        int employees = 10,
        decimal gross = 1_000_000m,
        decimal employerCost = 0m,
        DateTime? approvedAt = null,
        DateTime? journalPostedAt = null) => new()
        {
            Id = id,
            RunNumber = id,
            PayrollPeriodCode = periodStart.ToString("yyyy-MM"),
            PeriodStart = periodStart,
            PeriodEnd = periodStart.AddMonths(1).AddDays(-1),
            Status = "Approved",
            EmployeeCount = employees,
            TotalGross = gross,
            TotalEmployerCost = employerCost,
            ApprovedAt = approvedAt,
            JournalPostedAt = journalPostedAt,
        };

    // ── What the report asks HR for ──────────────────────────────────────────────

    [Fact]
    public async Task Omitting_the_status_asks_for_approved_runs_only()
    {
        var hr = new StubHr();

        await Sut(hr).GetAsync(null);

        // A draft or computed run's figures were never paid to anyone. Defaulting to "everything"
        // would report money that was never spent as payroll cost.
        hr.StatusAsked.Should().Be("Approved");
    }

    [Fact]
    public async Task An_explicit_status_is_passed_through_rather_than_overridden()
    {
        var hr = new StubHr();

        await Sut(hr).GetAsync("Computed");

        hr.StatusAsked.Should().Be("Computed");
    }

    // ── The three figures that are not the obvious sum ───────────────────────────

    [Fact]
    public async Task Headcount_is_the_latest_runs_and_not_the_sum_across_runs()
    {
        var hr = new StubHr
        {
            Runs = new()
            {
                Run("jan", new DateTime(2026, 1, 1), employees: 10),
                Run("feb", new DateTime(2026, 2, 1), employees: 12),
            },
        };

        var report = await Sut(hr).GetAsync(null);

        // 22 would be the same people counted twice. 10 would be reading the wrong end of the list.
        report.Totals.LatestHeadcount.Should().Be(12);
    }

    [Fact]
    public async Task Runs_are_returned_most_recent_first()
    {
        var hr = new StubHr
        {
            Runs = new()
            {
                Run("jan", new DateTime(2026, 1, 1)),
                Run("mar", new DateTime(2026, 3, 1)),
                Run("feb", new DateTime(2026, 2, 1)),
            },
        };

        var report = await Sut(hr).GetAsync(null);

        // Asserted separately from headcount because LatestHeadcount depends on this ordering: it
        // reads the first element and would silently take January's if the sort were reversed.
        report.Runs.Select(r => r.Id).Should().ContainInOrder("mar", "feb", "jan");
    }

    [Fact]
    public async Task Cost_of_employment_adds_the_employers_own_contributions_to_gross()
    {
        var hr = new StubHr
        {
            Runs = new()
            {
                Run("jan", new DateTime(2026, 1, 1), gross: 1_000_000m, employerCost: 40_000m),
                Run("feb", new DateTime(2026, 2, 1), gross: 1_100_000m, employerCost: 44_000m),
            },
        };

        var report = await Sut(hr).GetAsync(null);

        report.Totals.TotalGross.Should().Be(2_100_000m);
        report.Totals.TotalEmployerCost.Should().Be(84_000m);
        // The point of the report. Gross alone understates the cost by the employer's NSSF and
        // housing levy, which is exactly the mistake this figure exists to prevent.
        report.Totals.TotalCostOfEmployment.Should().Be(2_184_000m);
        report.Totals.TotalCostOfEmployment.Should().BeGreaterThan(report.Totals.TotalGross);
    }

    [Fact]
    public async Task Only_approved_runs_whose_journal_never_posted_are_counted_as_unposted()
    {
        var hr = new StubHr
        {
            Runs = new()
            {
                // Approved, journal missing — the one case that matters: staff are owed money the
                // ledger has no record of.
                Run("jan", new DateTime(2026, 1, 1),
                    approvedAt: new DateTime(2026, 2, 1), journalPostedAt: null),
                // Approved and posted — nothing wrong.
                Run("feb", new DateTime(2026, 2, 1),
                    approvedAt: new DateTime(2026, 3, 1), journalPostedAt: new DateTime(2026, 3, 1)),
                // Never approved, so no journal is expected yet. Counting this would cry wolf on
                // every draft run and train people to ignore the figure.
                Run("mar", new DateTime(2026, 3, 1), approvedAt: null, journalPostedAt: null),
            },
        };

        var report = await Sut(hr).GetAsync(null);

        report.Totals.RunsWithUnpostedJournal.Should().Be(1);
    }

    // ── Degraded upstream ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_HR_failure_degrades_to_a_warning_rather_than_a_500()
    {
        var hr = new StubHr { Throw = new HttpRequestException("hr is down") };

        var report = await Sut(hr).GetAsync(null);

        report.Warnings.Should().ContainSingle().Which.Should().Contain("payroll runs");
        report.Runs.Should().BeEmpty();
        // Zero, not garbage: a report that renders with a visible warning beats a 502 the caller
        // cannot distinguish from a bug in reporting itself.
        report.Totals.RunCount.Should().Be(0);
        report.Totals.TotalCostOfEmployment.Should().Be(0m);
    }

    [Fact]
    public async Task A_null_body_is_treated_as_no_runs_rather_than_throwing()
    {
        // HttpClient JSON deserialisation returns null for a `null` body, and every other report in
        // this service handles that. A NullReferenceException here would surface as a 502 whose log
        // says nothing useful.
        var hr = new StubHr { Runs = null };

        var report = await Sut(hr).GetAsync(null);

        report.Runs.Should().BeEmpty();
        report.Totals.RunCount.Should().Be(0);
        report.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task No_runs_is_a_clean_empty_report_and_not_a_warning()
    {
        var hr = new StubHr { Runs = new() };

        var report = await Sut(hr).GetAsync(null);

        // A tenant that has not run payroll yet is not an error condition.
        hr.WasCalled.Should().BeTrue();
        report.Warnings.Should().BeEmpty();
        report.Totals.LatestHeadcount.Should().Be(0);
    }
}
