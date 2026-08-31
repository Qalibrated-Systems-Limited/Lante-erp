using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// The depreciation run's double-post guard, against a real PostgreSQL (#230).
///
/// <para>These are the two bullets #230 listed that <see cref="DepreciationRulesTests"/> could not
/// reach: <i>"running it twice for the same period does not double-post"</i> and <i>"disposal stops
/// further depreciation"</i>. Both are database-level. <c>RunDepreciationAsync</c> opens a
/// transaction and takes a <c>pg_advisory_xact_lock</c>; the in-memory provider has no transactions
/// and no raw SQL, and SQLite has no such function, so until now the guard that stops a month's
/// depreciation posting twice had never been executed by anything.</para>
///
/// <para>A duplicated depreciation journal is not a cosmetic error: it doubles the charge to the P&amp;L
/// and the credit to accumulated depreciation, and the second one looks exactly as legitimate as the
/// first.</para>
/// </summary>
public class DepreciationConcurrencyTests
{
    // ── Idempotency ─────────────────────────────────────────────────────────────

    [PostgresFact]
    public async Task Running_a_period_twice_does_not_post_twice()
    {
        await using var f = await PostgresFixture.CreateAsync();
        var period = PostgresFixture.CurrentPeriod();

        var first = await f.Depreciation.RunDepreciationAsync(period, "alice");
        var second = await f.Depreciation.RunDepreciationAsync(period, "alice");

        first.AlreadyRun.Should().BeFalse();
        second.AlreadyRun.Should().BeTrue("the second call must recognise the period is done");

        // The assertion that matters is the ledger, not the flag: a run that reported AlreadyRun
        // while still posting would satisfy a flag-only test.
        (await f.Db.JournalEntries.CountAsync(e => e.SourceModule == "FixedAssets")).Should().Be(1);
        (await f.Db.DepreciationEntries.CountAsync(e => e.Period == period)).Should().Be(1);
    }

    [PostgresFact]
    public async Task The_second_run_reports_the_first_runs_figures_rather_than_zero()
    {
        await using var f = await PostgresFixture.CreateAsync();
        var period = PostgresFixture.CurrentPeriod();

        var first = await f.Depreciation.RunDepreciationAsync(period, "alice");
        var second = await f.Depreciation.RunDepreciationAsync(period, "alice");

        // 1,200,000 at 20% a year = 20,000 a month.
        first.TotalCharge.Should().Be(20_000m);
        second.TotalCharge.Should().Be(first.TotalCharge);
        second.AssetsProcessed.Should().Be(first.AssetsProcessed);
        second.JournalEntryId.Should().Be(first.JournalEntryId);
    }

    [PostgresFact]
    public async Task Accumulated_depreciation_moves_once_not_twice()
    {
        await using var f = await PostgresFixture.CreateAsync();
        var period = PostgresFixture.CurrentPeriod();

        await f.Depreciation.RunDepreciationAsync(period, "alice");
        await f.Depreciation.RunDepreciationAsync(period, "alice");
        f.Db.ChangeTracker.Clear();

        var asset = await f.Db.FixedAssets.SingleAsync(a => a.Id == PostgresFixture.AssetId);
        asset.AccumulatedDepreciation.Should().Be(20_000m);
    }

    [PostgresFact]
    public async Task Concurrent_runs_for_the_same_period_still_post_only_one_journal()
    {
        await using var f = await PostgresFixture.CreateAsync();
        var period = PostgresFixture.CurrentPeriod();

        // The case the advisory lock exists for, and the reason the check-then-act above it is safe:
        // the manual "Run Depreciation" button racing the monthly background tick, or two pod
        // replicas ticking together. Separate contexts, because a single DbContext is not
        // thread-safe and sharing one would test something else entirely.
        await using var second = await PostgresFixture.CreateForSameDatabaseAsync(f);

        var a = f.Depreciation.RunDepreciationAsync(period, "alice");
        var b = second.Depreciation.RunDepreciationAsync(period, "bob");
        await Task.WhenAll(a, b);

        f.Db.ChangeTracker.Clear();
        (await f.Db.JournalEntries.CountAsync(e => e.SourceModule == "FixedAssets")).Should().Be(1);
        (await f.Db.DepreciationEntries.CountAsync(e => e.Period == period)).Should().Be(1);
        // Exactly one of the two did the work.
        new[] { (await a).AlreadyRun, (await b).AlreadyRun }.Should().ContainSingle(x => x == false);
    }

    // ── Disposal ────────────────────────────────────────────────────────────────

    [PostgresFact]
    public async Task A_disposed_asset_stops_depreciating()
    {
        await using var f = await PostgresFixture.CreateAsync();

        var asset = await f.Db.FixedAssets.SingleAsync(a => a.Id == PostgresFixture.AssetId);
        asset.Status = "Disposed";
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var result = await f.Depreciation.RunDepreciationAsync(PostgresFixture.CurrentPeriod(), "alice");

        // The run filters on Status == "Active". A disposed asset that kept depreciating would
        // charge the P&L for something the business no longer owns.
        result.AssetsProcessed.Should().Be(0);
        result.TotalCharge.Should().Be(0m);
        (await f.Db.JournalEntries.CountAsync(e => e.SourceModule == "FixedAssets")).Should().Be(0);
    }

    [PostgresFact]
    public async Task A_fully_depreciated_asset_is_skipped_without_posting_an_empty_journal()
    {
        await using var f = await PostgresFixture.CreateAsync();

        var asset = await f.Db.FixedAssets.SingleAsync(a => a.Id == PostgresFixture.AssetId);
        asset.AccumulatedDepreciation = asset.AcquisitionCost;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var result = await f.Depreciation.RunDepreciationAsync(PostgresFixture.CurrentPeriod(), "alice");

        // Nothing to charge, so nothing should be posted — a zero-value journal would trip
        // JournalService's own "total must be greater than zero" guard and fail the whole run.
        result.AssetsProcessed.Should().Be(0);
        (await f.Db.JournalEntries.CountAsync()).Should().Be(0);
    }
}
