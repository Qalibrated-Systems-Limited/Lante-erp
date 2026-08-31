using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;
using ReportingService.Infrastructure.Services;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// Report #14 — Revenue vs KPI Target by Department (#225), the fourteenth of fifteen.
///
/// <para>Two things carry the risk here, and neither is the grouping itself.</para>
///
/// <para><b>Company-wide must not be added to the departments.</b> Finance's revenue targets use
/// <c>Scope</c> for the department, and one of those scopes is the whole business. Rolling it into the
/// departmental total would double the target against the same revenue.</para>
///
/// <para><b>The actual cannot be summed.</b> <c>ListTargetsAsync</c> filters on fiscal year alone and
/// <c>RevenueTarget</c> has no version, so a department can hold several target rows; each row's
/// actual is computed from its own cost centre, which the read DTO does not expose. So duplicate rows
/// are indistinguishable from two real cost centres, and adding their actuals would report revenue
/// earned once as earned twice. Same root cause as #347 on the budget side.</para>
/// </summary>
public class RevenueVsTargetReportTests
{
    private sealed class StubFinance : IFinanceServiceClient
    {
        public List<RevenueTargetReadDto>? Targets { get; set; } = new();
        public List<FiscalYearDto>? Years { get; set; } = new();
        public string? FiscalYearAsked { get; private set; }
        public Exception? Throw { get; set; }

        public Task<List<RevenueTargetReadDto>?> GetRevenueTargetsAsync(string fiscalYearId)
        {
            FiscalYearAsked = fiscalYearId;
            if (Throw != null) throw Throw;
            return Task.FromResult(Targets);
        }

        public Task<List<FiscalYearDto>?> GetFiscalYearsAsync() => Task.FromResult(Years);

        public Task<ProfitLossDto?> GetPnlAsync(string periodId) => throw new NotSupportedException();
        public Task<TrialBalanceDto?> GetTrialBalanceAsync(DateTime? asOf) => throw new NotSupportedException();
        public Task<List<BudgetReadDto>?> GetBudgetsAsync(string fiscalYearId) => throw new NotSupportedException();
        public Task<List<DebtorAgingRowDto>?> GetAgedDebtorsAsync(DateTime? asOf) => throw new NotSupportedException();
        public Task<CashFlowDto?> GetCashFlowForecastAsync(DateTime? asOf, int weeks) => throw new NotSupportedException();
        public Task<List<FixedAssetRowDto>?> GetFixedAssetsAsync() => throw new NotSupportedException();
        public Task<List<DepreciationEntryRowDto>?> GetDepreciationScheduleAsync(string? period) => throw new NotSupportedException();
        public Task<List<AssetDisposalRowDto>?> GetAssetDisposalsAsync() => throw new NotSupportedException();
    }

    private static RevenueVsTargetReportService Sut(StubFinance f) =>
        new(f, NullLogger<RevenueVsTargetReportService>.Instance);

    private static RevenueTargetReadDto Target(string scope, decimal target, decimal actual, string status = "") => new()
    {
        Id = Guid.NewGuid().ToString(), Scope = scope, AnnualAmount = target, Actual = actual,
        Variance = actual - target, Status = status,
    };

    // ── Company-wide is held apart ──────────────────────────────────────────────

    [Fact]
    public async Task The_company_wide_target_is_not_counted_as_a_department()
    {
        var f = new StubFinance
        {
            Targets = new()
            {
                Target("Company-wide", 10_000_000m, 6_000_000m),
                Target("Calibration", 4_000_000m, 3_000_000m),
                Target("Fleet", 6_000_000m, 2_500_000m),
            },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        r.CompanyWide.Should().NotBeNull();
        r.CompanyWide!.Target.Should().Be(10_000_000m);
        // Two departments, not three, and their total excludes the company figure that already
        // covers them — otherwise the business appears to owe 20m of revenue against 10m of target.
        r.Departments.Should().HaveCount(2);
        r.Totals.DepartmentCount.Should().Be(2);
        r.Totals.TotalDepartmentTarget.Should().Be(10_000_000m);
        r.Totals.TotalDepartmentActual.Should().Be(5_500_000m);
    }

    [Fact]
    public async Task The_company_wide_scope_is_matched_regardless_of_casing()
    {
        var f = new StubFinance { Targets = new() { Target("company-wide", 1_000m, 500m) } };

        var r = await Sut(f).GetAsync("fy-2026");

        // Finance stores Scope as free text, so the label arrives however it was typed.
        r.CompanyWide.Should().NotBeNull();
        r.Departments.Should().BeEmpty();
    }

    [Fact]
    public async Task With_no_company_wide_target_the_departments_still_roll_up()
    {
        var f = new StubFinance
        {
            Targets = new() { Target("Calibration", 4_000_000m, 4_200_000m) },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        r.CompanyWide.Should().BeNull();
        r.Totals.TotalDepartmentTarget.Should().Be(4_000_000m);
        r.Totals.DepartmentsBehind.Should().Be(0);
    }

    // ── The actual is taken once, never summed ──────────────────────────────────

    [Fact]
    public async Task Several_targets_for_one_department_sum_the_target_but_not_the_actual()
    {
        var f = new StubFinance
        {
            Targets = new()
            {
                Target("Calibration", 3_000_000m, 2_500_000m),
                Target("Calibration", 1_000_000m, 2_500_000m),
            },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        var dept = r.Departments.Should().ContainSingle().Subject;
        dept.Target.Should().Be(4_000_000m);
        // 2.5m, NOT 5m. The two rows report the same cost centre's revenue, and the payload gives no
        // way to tell that from two genuinely different cost centres — so adding would turn revenue
        // earned once into revenue earned twice.
        dept.Actual.Should().Be(2_500_000m);
        dept.TargetRowCount.Should().Be(2);
    }

    [Fact]
    public async Task A_department_with_several_targets_is_warned_about_rather_than_silently_aggregated()
    {
        var f = new StubFinance
        {
            Targets = new()
            {
                Target("Calibration", 3_000_000m, 2_500_000m),
                Target("Calibration", 1_000_000m, 2_500_000m),
            },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        // The number shown is defensible but not certainly right, and the reader has to know that.
        r.Warnings.Should().ContainSingle();
        r.Warnings[0].Should().Contain("Calibration").And.Contain("#347");
    }

    [Fact]
    public async Task A_single_target_per_department_produces_no_warning()
    {
        var f = new StubFinance
        {
            Targets = new() { Target("Calibration", 1m, 1m), Target("Fleet", 1m, 1m) },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        // The warning must mean something. Firing on the ordinary case would train people past it.
        r.Warnings.Should().BeEmpty();
    }

    // ── Achievement ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Achieved_percentage_is_actual_over_target()
    {
        var f = new StubFinance { Targets = new() { Target("Calibration", 4_000_000m, 3_000_000m) } };

        var r = await Sut(f).GetAsync("fy-2026");

        r.Departments[0].AchievedPct.Should().Be(75.00m);
        r.Departments[0].Variance.Should().Be(-1_000_000m);
    }

    [Fact]
    public async Task A_zero_target_reports_no_percentage_rather_than_zero_percent()
    {
        var f = new StubFinance { Targets = new() { Target("New Line", 0m, 500_000m) } };

        var r = await Sut(f).GetAsync("fy-2026");

        // 0% would read as "achieved nothing" when the truth is "nothing was asked for" — and the
        // division itself is undefined.
        r.Departments[0].AchievedPct.Should().BeNull();
        r.Departments[0].Status.Should().Be("OnTrack");
    }

    [Fact]
    public async Task Finances_own_status_is_preserved_when_a_department_has_one_target()
    {
        // 50 against 100 — this report's own Grade() would call that "Behind", so finance saying
        // "OnTrack" is the only way to tell whether its grade was preserved or silently recomputed.
        // The first version of this test used a case where both agreed, and passed against a mutant
        // that threw finance's status away.
        var f = new StubFinance { Targets = new() { Target("Calibration", 100m, 50m, status: "OnTrack") } };

        var r = await Sut(f).GetAsync("fy-2026");

        // Finance graded it, and it may weight seasonality or partial-year phasing that this report
        // cannot see. Re-grading would put two different verdicts on the same row across two screens.
        r.Departments[0].Status.Should().Be("OnTrack");
    }

    [Fact]
    public async Task Status_is_recomputed_when_several_targets_are_combined()
    {
        var f = new StubFinance
        {
            Targets = new()
            {
                Target("Calibration", 100m, 200m, status: "Achieved"),
                Target("Calibration", 400m, 200m, status: "Behind"),
            },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        // Neither row's status describes the combined 500 vs 200 shown, so carrying one through would
        // label the row with a grade for a different number.
        r.Departments[0].Status.Should().Be("Behind");
    }

    [Fact]
    public async Task Departments_behind_target_are_counted()
    {
        var f = new StubFinance
        {
            Targets = new()
            {
                Target("Calibration", 100m, 120m),
                Target("Fleet", 100m, 80m),
                Target("Stores", 100m, 100m),
            },
        };

        var r = await Sut(f).GetAsync("fy-2026");

        // Exactly on target is not behind — a `<=` here would report every department that hit its
        // number as having missed it.
        r.Totals.DepartmentsBehind.Should().Be(1);
    }

    // ── Fiscal year resolution and degradation ──────────────────────────────────

    [Fact]
    public async Task Omitting_the_fiscal_year_resolves_the_current_one()
    {
        var today = DateTime.UtcNow;
        var f = new StubFinance
        {
            Years = new()
            {
                new FiscalYearDto { Id = "fy-old", StartDate = today.AddYears(-2), EndDate = today.AddYears(-1) },
                new FiscalYearDto { Id = "fy-now", StartDate = today.AddDays(-10), EndDate = today.AddDays(10) },
            },
        };

        var r = await Sut(f).GetAsync(null);

        f.FiscalYearAsked.Should().Be("fy-now");
        r.FiscalYearId.Should().Be("fy-now");
    }

    [Fact]
    public async Task No_fiscal_years_configured_degrades_to_a_warning_rather_than_a_500()
    {
        var f = new StubFinance { Years = new() };

        var r = await Sut(f).GetAsync(null);

        r.Warnings.Should().ContainSingle().Which.Should().Contain("No fiscal years");
        r.Departments.Should().BeEmpty();
    }

    [Fact]
    public async Task A_finance_failure_degrades_to_a_warning()
    {
        var f = new StubFinance { Throw = new HttpRequestException("finance is down") };

        var r = await Sut(f).GetAsync("fy-2026");

        r.Warnings.Should().ContainSingle().Which.Should().Contain("revenue targets");
        r.Totals.TotalDepartmentTarget.Should().Be(0m);
    }

    [Fact]
    public async Task A_blank_scope_is_grouped_rather_than_dropped()
    {
        var f = new StubFinance { Targets = new() { Target("", 1_000m, 400m), Target("   ", 500m, 100m) } };

        var r = await Sut(f).GetAsync("fy-2026");

        // Scope is free text and can arrive empty. Dropping those rows would quietly lose a target
        // from the roll-up; labelling them makes the data problem visible instead.
        r.Departments.Should().ContainSingle();
        r.Departments[0].Department.Should().Be("(unscoped)");
        r.Departments[0].Target.Should().Be(1_500m);
    }
}
