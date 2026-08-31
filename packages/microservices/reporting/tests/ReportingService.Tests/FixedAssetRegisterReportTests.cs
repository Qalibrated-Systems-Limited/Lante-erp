using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;
using ReportingService.Infrastructure.Services;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// Report #11 — Fixed Asset Register &amp; Depreciation Schedule (#225), the eleventh of fifteen.
///
/// <para>The arithmetic is the point. A register report that adds up wrong is worse than a missing one,
/// because it looks authoritative and feeds the balance sheet. The two figures most easily confused are
/// accumulated depreciation and the charge for the period — they differ by orders of magnitude on an
/// established register — so they are computed from different sources and asserted separately.</para>
///
/// <para>The finance client is stubbed rather than reached over HTTP: what is under test is the
/// aggregation and the partial-failure behaviour, not the transport.</para>
/// </summary>
public class FixedAssetRegisterReportTests
{
    private static FixedAssetRegisterReportService Sut(StubFinanceClient client) =>
        new(client, NullLogger<FixedAssetRegisterReportService>.Instance);

    private static FixedAssetRowDto Asset(string tag, string categoryId, string categoryName,
                                          decimal cost, decimal accumulated) => new()
    {
        Id = $"a-{tag}", AssetTag = tag, CategoryId = categoryId, CategoryName = categoryName,
        Description = tag, AcquisitionCost = cost,
        AccumulatedDepreciation = accumulated, NetBookValue = cost - accumulated,
        AcquisitionDate = new DateTime(2024, 1, 1), Status = "Active",
    };

    // ── The totals ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_register_totals_sum_the_assets()
    {
        var client = new StubFinanceClient
        {
            Assets = [Asset("TRK-1", "veh", "Vehicles", 4_000_000m, 1_000_000m),
                      Asset("LAP-1", "it", "IT Equipment", 150_000m, 90_000m)],
        };

        var report = await Sut(client).GetAsync(null);

        report.Summary.AssetCount.Should().Be(2);
        report.Summary.TotalAcquisitionCost.Should().Be(4_150_000m);
        report.Summary.TotalAccumulatedDepreciation.Should().Be(1_090_000m);
        report.Summary.TotalNetBookValue.Should().Be(3_060_000m);
    }

    [Fact]
    public async Task Net_book_value_equals_cost_less_accumulated()
    {
        var client = new StubFinanceClient
        {
            Assets = [Asset("A", "c", "C", 1_000m, 250m), Asset("B", "c", "C", 500m, 500m)],
        };

        var report = await Sut(client).GetAsync(null);

        // The identity the balance sheet depends on. A fully depreciated asset carries nil, not negative.
        (report.Summary.TotalAcquisitionCost - report.Summary.TotalAccumulatedDepreciation)
            .Should().Be(report.Summary.TotalNetBookValue);
    }

    [Fact]
    public async Task The_period_charge_is_NOT_the_accumulated_total()
    {
        var client = new StubFinanceClient
        {
            Assets = [Asset("TRK-1", "veh", "Vehicles", 4_000_000m, 1_000_000m)],
            Schedule = [new DepreciationEntryRowDto { AssetId = "a-TRK-1", Period = "2026-08", Amount = 66_666m }],
        };

        var report = await Sut(client).GetAsync("2026-08");

        // These two get confused constantly and differ by orders of magnitude on an established
        // register. Computed from different sources, named differently, asserted separately.
        report.Summary.DepreciationChargedInPeriod.Should().Be(66_666m);
        report.Summary.TotalAccumulatedDepreciation.Should().Be(1_000_000m);
        report.Summary.Period.Should().Be("2026-08");
    }

    // ── Category grouping ────────────────────────────────────────────────────────

    [Fact]
    public async Task Assets_are_grouped_by_category_and_the_groups_reconcile_to_the_total()
    {
        var client = new StubFinanceClient
        {
            Assets = [Asset("TRK-1", "veh", "Vehicles", 4_000_000m, 1_000_000m),
                      Asset("TRK-2", "veh", "Vehicles", 3_000_000m, 500_000m),
                      Asset("LAP-1", "it", "IT Equipment", 150_000m, 90_000m)],
        };

        var report = await Sut(client).GetAsync(null);

        report.ByCategory.Should().HaveCount(2);
        // A category breakdown that does not add back to the total is the classic way a register
        // report loses an asset without anyone noticing.
        report.ByCategory.Sum(c => c.NetBookValue).Should().Be(report.Summary.TotalNetBookValue);
        report.ByCategory.Sum(c => c.AssetCount).Should().Be(report.Summary.AssetCount);

        var vehicles = report.ByCategory.Single(c => c.CategoryName == "Vehicles");
        vehicles.AssetCount.Should().Be(2);
        vehicles.AcquisitionCost.Should().Be(7_000_000m);
    }

    [Fact]
    public async Task Categories_come_back_in_name_order()
    {
        var client = new StubFinanceClient
        {
            Assets = [Asset("Z", "z", "Vehicles", 1m, 0m), Asset("A", "a", "Buildings", 1m, 0m)],
        };

        var report = await Sut(client).GetAsync(null);

        report.ByCategory.Select(c => c.CategoryName).Should().Equal("Buildings", "Vehicles");
    }

    [Fact]
    public async Task Two_categories_sharing_a_display_name_stay_separate()
    {
        var client = new StubFinanceClient
        {
            // Finance keys categories by id; nothing stops two carrying the same label — a renamed
            // category alongside its replacement, most likely.
            Assets = [Asset("A", "cat-old", "Vehicles", 1_000m, 0m),
                      Asset("B", "cat-new", "Vehicles", 2_000m, 0m)],
        };

        var report = await Sut(client).GetAsync(null);

        // Grouping on the NAME alone would merge them into one row whose CategoryId is whichever won —
        // a figure attributed to a category it does not belong to. Mutation testing found this gap: I had
        // no test that distinguished grouping by (id, name) from grouping by name.
        report.ByCategory.Should().HaveCount(2);
        report.ByCategory.Select(c => c.CategoryId).Should().BeEquivalentTo(["cat-old", "cat-new"]);
        report.ByCategory.Sum(c => c.AcquisitionCost).Should().Be(3_000m);
    }

    // ── Disposals ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_disposal_above_book_value_is_a_gain_and_below_is_a_loss()
    {
        var client = new StubFinanceClient
        {
            Disposals =
            [
                new AssetDisposalRowDto { Id = "d1", Proceeds = 500_000m, ClosingNbv = 300_000m, MdApprovedBy = "md" },
                new AssetDisposalRowDto { Id = "d2", Proceeds = 100_000m, ClosingNbv = 250_000m, MdApprovedBy = "md" },
            ],
        };

        var report = await Sut(client).GetAsync(null);

        // +200,000 and -150,000. Signed, because the sign is the whole message — an absolute figure here
        // would report a 350,000 swing as if it were a gain.
        report.Summary.DisposalGainOrLoss.Should().Be(50_000m);
        report.Summary.DisposalProceeds.Should().Be(600_000m);
        report.Summary.DisposalCount.Should().Be(2);
    }

    [Fact]
    public async Task A_disposal_with_no_proceeds_is_a_full_loss_not_a_gap()
    {
        var client = new StubFinanceClient
        {
            Disposals = [new AssetDisposalRowDto { Id = "d1", Proceeds = null, ClosingNbv = 80_000m, MdApprovedBy = "md" }],
        };

        var report = await Sut(client).GetAsync(null);

        // Scrapped or donated: null proceeds is zero received against a real book value, which is a loss
        // of the whole NBV. Treating null as "unknown" and skipping it would understate the loss.
        report.Summary.DisposalGainOrLoss.Should().Be(-80_000m);
        report.Summary.DisposalProceeds.Should().Be(0m);
    }

    [Fact]
    public async Task Disposals_awaiting_approval_are_counted()
    {
        var client = new StubFinanceClient
        {
            Disposals =
            [
                new AssetDisposalRowDto { Id = "d1", MdApprovedBy = null },
                new AssetDisposalRowDto { Id = "d2", MdApprovedBy = "md", RequiresBoardApproval = true, BoardApprovedBy = null },
                new AssetDisposalRowDto { Id = "d3", MdApprovedBy = "md", RequiresBoardApproval = true, BoardApprovedBy = "board" },
                new AssetDisposalRowDto { Id = "d4", MdApprovedBy = "md", RequiresBoardApproval = false },
            ],
        };

        var report = await Sut(client).GetAsync(null);

        // d1 has no MD approval; d2 needs the board and has not got it. An asset pending disposal
        // approval is still on the books and its NBV is still in the totals, so the count belongs here.
        report.Summary.DisposalsPendingApproval.Should().Be(2);
    }

    // ── Partial failure ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_failing_call_warns_and_the_rest_of_the_report_still_builds()
    {
        var client = new StubFinanceClient
        {
            Assets = [Asset("TRK-1", "veh", "Vehicles", 4_000_000m, 1_000_000m)],
            ThrowOnDisposals = true,
        };

        var report = await Sut(client).GetAsync(null);

        // Finance being partially unavailable must not blank the register. Same posture as the other
        // client-backed reports — a warning, not an exception.
        report.Assets.Should().ContainSingle();
        report.Summary.TotalNetBookValue.Should().Be(3_000_000m);
        report.Disposals.Should().BeEmpty();
        report.Warnings.Should().ContainSingle().Which.Should().Contain("disposals");
    }

    [Fact]
    public async Task A_null_response_is_treated_as_empty_rather_than_crashing()
    {
        var report = await Sut(new StubFinanceClient()).GetAsync(null);

        report.Assets.Should().BeEmpty();
        report.Summary.AssetCount.Should().Be(0);
        report.Summary.TotalNetBookValue.Should().Be(0m);
        report.Warnings.Should().BeEmpty();
    }

    /// <summary>Only the three asset calls matter here; the rest of the interface is not exercised.</summary>
    private sealed class StubFinanceClient : IFinanceServiceClient
    {
        public List<FixedAssetRowDto>? Assets { get; set; }
        public List<DepreciationEntryRowDto>? Schedule { get; set; }
        public List<AssetDisposalRowDto>? Disposals { get; set; }
        public bool ThrowOnDisposals { get; set; }

        public Task<List<FixedAssetRowDto>?> GetFixedAssetsAsync() => Task.FromResult(Assets);
        public Task<List<DepreciationEntryRowDto>?> GetDepreciationScheduleAsync(string? period) => Task.FromResult(Schedule);
        public Task<List<AssetDisposalRowDto>?> GetAssetDisposalsAsync() =>
            ThrowOnDisposals
                ? throw new HttpRequestException("finance unreachable")
                : Task.FromResult(Disposals);

        public Task<ProfitLossDto?> GetPnlAsync(string periodId) => Task.FromResult<ProfitLossDto?>(null);
        public Task<TrialBalanceDto?> GetTrialBalanceAsync(DateTime? asOf) => Task.FromResult<TrialBalanceDto?>(null);
        public Task<List<BudgetReadDto>?> GetBudgetsAsync(string fiscalYearId) => Task.FromResult<List<BudgetReadDto>?>(null);
        public Task<List<RevenueTargetReadDto>?> GetRevenueTargetsAsync(string fiscalYearId) => Task.FromResult<List<RevenueTargetReadDto>?>(null);
        public Task<List<DebtorAgingRowDto>?> GetAgedDebtorsAsync(DateTime? asOf) => Task.FromResult<List<DebtorAgingRowDto>?>(null);
        public Task<CashFlowDto?> GetCashFlowForecastAsync(DateTime? asOf, int weeks) => Task.FromResult<CashFlowDto?>(null);
        public Task<List<FiscalYearDto>?> GetFiscalYearsAsync() => Task.FromResult<List<FiscalYearDto>?>(null);
    }
}
