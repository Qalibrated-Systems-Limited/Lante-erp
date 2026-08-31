using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.Enums;
using Xunit;

namespace ProcurementService.Tests;

/// <summary>
/// The three-way match — goods received, invoice present, value agrees — which is the control that
/// stands between a supplier invoice and a payment voucher. Procurement had no tests at all (#247).
///
/// <para>Tolerance is 1% of the order value, floored at KES 1 (`TolerancePercent`, `MinTolerance`).</para>
/// </summary>
public class ThreeWayMatchTests
{
    // ── Matching ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_exact_invoice_on_a_fully_received_order_matches()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(100_000m);

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        result.Status.Should().Be("Matched");
        (await f.Db.MatchingExceptions.CountAsync(e => e.Status == MatchExceptionStatus.Open)).Should().Be(0);
    }

    [Fact]
    public async Task A_difference_inside_tolerance_still_matches()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(100_500m);   // +0.5%, inside the 1% tolerance of 1,000

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // Rounding and cent-level differences must not raise an exception a human has to clear, or
        // the control becomes noise and gets waved through.
        result.Status.Should().Be("Matched");
    }

    [Fact]
    public async Task On_a_small_order_the_tolerance_floor_applies_rather_than_the_percentage()
    {
        using var f = new MatchFixture(poTotal: 50m);
        f.InvoiceFor(50.80m);     // +0.80, over 1% (0.50) but under the KES 1 floor

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // 1% of a small order is pennies, so without the floor every rounding difference on a cheap
        // LPO would block payment.
        result.Status.Should().Be("Matched");
    }

    [Fact]
    public async Task An_invoice_above_the_order_beyond_tolerance_raises_TotalExceedsPo()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(105_000m);

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        result.Status.Should().Be("Exception");
        var ex = await f.Db.MatchingExceptions.SingleAsync();
        // Overbilling is the failure this control exists for.
        ex.ExceptionType.Should().Be(MatchExceptionType.TotalExceedsPo);
        ex.Status.Should().Be(MatchExceptionStatus.Open);
    }

    [Fact]
    public async Task An_invoice_BELOW_the_order_beyond_tolerance_also_raises_an_exception()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(60_000m);

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // Under-billing is not a windfall — it usually means a partial delivery invoiced against a
        // full order, and paying it silently closes the LPO for the remainder. PriceOk is strictly
        // stronger than TotalOk, which is what makes this case reachable at all.
        result.Status.Should().Be("Exception");
        (await f.Db.MatchingExceptions.SingleAsync()).ExceptionType.Should().Be(MatchExceptionType.PriceMismatch);
    }

    // ── Receipt and invoice presence ─────────────────────────────────────────────

    [Fact]
    public async Task An_order_not_fully_received_cannot_match_however_correct_the_invoice()
    {
        using var f = new MatchFixture(poTotal: 100_000m, receipt: PoReceiptStatus.PartiallyReceived);
        f.InvoiceFor(100_000m);

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        result.Status.Should().Be("Exception");
        (await f.Db.MatchingExceptions.AnyAsync(e => e.ExceptionType == MatchExceptionType.NotFullyReceived))
            .Should().BeTrue();
    }

    [Fact]
    public async Task No_invoice_recorded_in_finance_is_its_own_exception()
    {
        using var f = new MatchFixture();
        // no InvoiceFor(...) — Finance has nothing against this LPO

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        result.Status.Should().Be("Exception");
        (await f.Db.MatchingExceptions.AnyAsync(e => e.ExceptionType == MatchExceptionType.NoInvoice)).Should().BeTrue();
    }

    [Fact]
    public async Task Two_independent_failures_raise_two_exceptions()
    {
        using var f = new MatchFixture(poTotal: 100_000m, receipt: PoReceiptStatus.NotReceived);
        // not received AND no invoice

        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // Reporting only the first would send someone to fix one problem and be surprised by the next.
        var types = await f.Db.MatchingExceptions.Select(e => e.ExceptionType).ToListAsync();
        types.Should().Contain(MatchExceptionType.NotFullyReceived);
        types.Should().Contain(MatchExceptionType.NoInvoice);
    }

    // ── Finance being unreachable is fail-CLOSED ─────────────────────────────────

    [Fact]
    public async Task An_unreachable_finance_blocks_the_match_rather_than_assuming_no_invoice()
    {
        using var f = new MatchFixture();
        f.Invoices.Reachable = false;

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // This seam is deliberately fail-closed, unlike the budget and journal seams: it guards a
        // payment. Treating "cannot ask Finance" as "Finance has no invoice" would raise a NoInvoice
        // exception that a human could then resolve — clearing the way to pay against nothing.
        result.Status.Should().Be("Error");
        result.Message.Should().Contain("Cannot match without Finance");
        (await f.Db.ThreeWayMatches.CountAsync()).Should().Be(0);
    }

    // ── Idempotence ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Re_running_a_match_updates_it_rather_than_creating_a_second()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(105_000m);
        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        f.InvoiceFor(100_000m);                     // the supplier issues a corrected invoice
        var second = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        second.Status.Should().Be("Matched");
        (await f.Db.ThreeWayMatches.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_resolved_failure_closes_its_exception_rather_than_leaving_it_open()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(105_000m);
        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        f.InvoiceFor(100_000m);
        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // An exception left open after the underlying problem is gone blocks payment forever and
        // teaches people to resolve exceptions without reading them.
        (await f.Db.MatchingExceptions.CountAsync(e => e.Status == MatchExceptionStatus.Open)).Should().Be(0);
    }

    // ── State guards ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_an_issued_order_can_be_matched()
    {
        using var f = new MatchFixture(status: PoStatus.Draft);
        f.InvoiceFor(100_000m);

        var result = await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        result.Status.Should().Be("Error");
        result.Message.Should().Contain("Only an issued LPO");
    }

    [Fact]
    public async Task An_unknown_order_is_an_error_not_a_silent_no_op()
    {
        using var f = new MatchFixture();

        var result = await f.Matches.RunAsync("does-not-exist", MatchFixture.Actor);

        result.Status.Should().Be("Error");
    }

    // ── The write-back to Finance ────────────────────────────────────────────────

    [Fact]
    public async Task The_match_outcome_is_written_back_to_the_supplier_invoice()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(100_000m);

        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        // Finance shows the match state on the invoice; without the write-back the two systems
        // disagree about whether the invoice is payable.
        f.Invoices.WriteBacks.Should().ContainSingle();
        f.Invoices.WriteBacks[0].Id.Should().Be("si-1");
    }

    // ── Queries that must translate to SQL ───────────────────────────────────────

    [Fact]
    public async Task The_summary_aggregates_translate_against_a_real_database()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(100_000m);
        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        var summary = await f.Matches.GetSummaryAsync();

        // GetSummaryAsync uses Contains over a materialised id list, CountAsync and a Sum. Those are
        // exactly the shapes an EF major-version bump changes — silently evaluating client-side, or
        // failing to translate at all. On EF InMemory this test would pass without ever building SQL,
        // which is why the fixture uses SQLite (#247, and the 8->9 bump in #227).
        summary.Matched.Should().Be(1);
        summary.MatchedValue.Should().Be(100_000m);
        summary.OpenExceptions.Should().Be(0);
    }

    [Fact]
    public async Task The_exception_listing_translates_and_filters()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(105_000m);
        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        var open = await f.Matches.GetExceptionsAsync("Open");
        var resolved = await f.Matches.GetExceptionsAsync("Resolved");

        open.Should().ContainSingle();
        resolved.Should().BeEmpty();
    }

    [Fact]
    public async Task The_match_list_projects_open_exception_counts_per_row()
    {
        using var f = new MatchFixture(poTotal: 100_000m);
        f.InvoiceFor(105_000m);
        await f.Matches.RunAsync(MatchFixture.PoId, MatchFixture.Actor);

        var list = await f.Matches.GetAllAsync(new Core.DTOs.Matching.MatchFilterParams());

        // The per-row count comes from a grouped query joined back onto the page in memory — a shape
        // that breaks loudly if the grouping stops translating.
        list.Items.Should().ContainSingle();
        list.Items[0].OpenExceptions.Should().Be(1);
    }
}
