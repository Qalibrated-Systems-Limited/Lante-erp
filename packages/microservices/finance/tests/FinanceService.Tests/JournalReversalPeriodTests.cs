using FinanceService.Core.DTOs;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Which accounting period a reversal posts into, and whether the close can stop it.
///
/// <para><b>Why this file exists.</b> Reversal is the only working undo in the whole finance service
/// — invoices and supplier invoices both have a <c>Cancelled</c> status that no code path can reach
/// (#332). It also walked straight through a closed period. The only closed-period guard lives in
/// <c>JournalService.CreateAsync</c>; <c>ReverseAsync</c> builds its entry by hand and calls
/// <c>PostInternalAsync</c>, which checks nothing. So a journal posted in June could be reversed
/// after June was closed, which makes closing a month a suggestion rather than a lock.</para>
///
/// <para>Found by tracing each write path into the period invariant separately instead of trusting
/// that "finance refuses to post into a closed period" — a claim an existing test does assert, but
/// only for the <c>CreateAsync</c> path. Confirmed by running it, not by reading: a probe that
/// closed the period after posting and then reversed threw nothing at all.</para>
///
/// <para>Second defect in the same few lines: the reversal stamped <c>PeriodId</c> from the ORIGINAL
/// entry while dating itself <c>UtcNow</c>, so the GL row carried today's date inside last period's
/// bucket. The fixture had exactly one period, June 2026, which made every reversal in the suite
/// incoherent that way — and nothing noticed, because nothing asserted the pair.</para>
/// </summary>
public class JournalReversalPeriodTests
{
    // ── The close is a lock ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_reversal_is_refused_when_the_current_period_is_closed()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);
        await SetCurrentPeriodStatusAsync(f, PeriodStatus.Closed);

        var act = () => f.Journals.ReverseAsync(posted.Id, "dave");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Closed*");
    }

    [Fact]
    public async Task A_reversal_is_refused_when_the_current_period_is_locked()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);
        await SetCurrentPeriodStatusAsync(f, PeriodStatus.Locked);

        // Locked is the stronger of the two states, so a guard written as `!= Closed` rather than
        // `!= Open` would let the year-end lock through while holding the monthly close.
        var act = () => f.Journals.ReverseAsync(posted.Id, "dave");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Locked*");
    }

    [Fact]
    public async Task Nothing_is_written_when_the_reversal_is_refused()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);
        var glBefore = await f.Db.GeneralLedgerEntries.CountAsync();
        await SetCurrentPeriodStatusAsync(f, PeriodStatus.Closed);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Journals.ReverseAsync(posted.Id, "dave"));
        f.Db.ChangeTracker.Clear();

        // A refusal that still leaves a Draft reversal entry behind, or flips the original to
        // Reversed, is worse than either outcome: the ledger then disagrees with the audit trail.
        (await f.Db.GeneralLedgerEntries.CountAsync()).Should().Be(glBefore);
        (await f.Db.JournalEntries.CountAsync(e => e.IsReversal)).Should().Be(0);
        (await f.Db.JournalEntries.SingleAsync(e => e.Id == posted.Id)).Status
            .Should().Be(JournalStatus.Posted);
    }

    [Fact]
    public async Task A_reversal_is_refused_when_no_period_covers_today()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);
        var current = await f.Db.AccountingPeriods.SingleAsync(p => p.Id == LedgerFixture.CurrentPeriodId);
        f.Db.AccountingPeriods.Remove(current);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var act = () => f.Journals.ReverseAsync(posted.Id, "dave");

        // Refused, not forced into whichever period happens to exist. Before the fix this silently
        // inherited the original's period, which is how a GL row ended up dated outside the period
        // it was filed under.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No accounting period covers*");
    }

    // ── Closing a month does not block the undo, it redirects it ─────────────────

    [Fact]
    public async Task A_journal_from_a_closed_month_still_reverses_into_the_open_current_period()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);

        // Month-end close on the period the original was posted into.
        var june = await f.Db.AccountingPeriods.SingleAsync(p => p.Id == "per-2026-06");
        june.Status = PeriodStatus.Closed;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var reversal = await f.Journals.ReverseAsync(posted.Id, "dave");

        // This is the correct treatment and the reason the guard reads today's period rather than the
        // original's: a mistake found after the close is corrected in the current open period, not by
        // reaching back into a closed one.
        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == reversal.Id);
        entry.PeriodId.Should().Be(LedgerFixture.CurrentPeriodId);
        entry.Status.Should().Be(JournalStatus.Posted);

        // And June is left exactly as the close found it.
        (await f.Db.GeneralLedgerEntries.CountAsync(g => g.PeriodId == "per-2026-06")).Should().Be(2);
    }

    // ── The date and the period agree ───────────────────────────────────────────

    [Fact]
    public async Task The_reversal_lands_in_the_period_that_covers_its_own_entry_date()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);

        var reversal = await f.Journals.ReverseAsync(posted.Id, "dave");

        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == reversal.Id);
        var period = await f.Db.AccountingPeriods.SingleAsync(p => p.Id == entry.PeriodId);

        // The invariant that was broken: EntryDate came from UtcNow while PeriodId came from the
        // original entry, so the two could point at different months with nothing reconciling them.
        entry.EntryDate.Should().BeOnOrAfter(period.StartDate).And.BeOnOrBefore(period.EndDate);
        entry.PeriodId.Should().NotBe("per-2026-06");
    }

    [Fact]
    public async Task The_GL_rows_carry_the_reversals_own_date_and_period_not_the_originals()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);

        var reversal = await f.Journals.ReverseAsync(posted.Id, "dave");

        var rows = await f.Db.GeneralLedgerEntries
            .Where(g => g.JournalEntryId == reversal.Id).ToListAsync();
        rows.Should().HaveCount(2);
        // Asserted on the GL rather than the journal header because the GL is what the trial balance
        // and every period report read. A correct header with mis-filed GL rows would still be wrong.
        rows.Should().OnlyContain(g => g.PeriodId == LedgerFixture.CurrentPeriodId);
        rows.Should().OnlyContain(g => g.EntryDate == DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task The_reversal_still_inverts_the_original_and_nets_to_zero()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f);

        var reversal = await f.Journals.ReverseAsync(posted.Id, "dave");

        // Redirecting the reversal to a different period must not disturb what a reversal IS. Netting
        // now spans two periods, which is exactly what a cross-period correction looks like.
        var original = await f.Db.JournalEntries.SingleAsync(e => e.Id == posted.Id);
        var reversed = await f.Db.JournalEntries.SingleAsync(e => e.Id == reversal.Id);
        reversed.TotalDebit.Should().Be(original.TotalCredit);
        reversed.TotalCredit.Should().Be(original.TotalDebit);
        reversed.ReversalOfId.Should().Be(original.Id);
        original.Status.Should().Be(JournalStatus.Reversed);

        var net = await f.Db.GeneralLedgerEntries
            .Where(g => g.JournalEntryId == original.Id || g.JournalEntryId == reversed.Id)
            .SumAsync(g => g.Debit - g.Credit);
        net.Should().Be(0m);
    }

    [Fact]
    public async Task Reversing_a_prior_year_journal_numbers_it_in_the_current_year()
    {
        using var f = new LedgerFixture();
        // Posted in December 2025, reversed today — an audit adjustment found after the year end,
        // which is the realistic way these two years come apart.
        var created = await f.Journals.CreateAsync(
            LedgerFixture.BalancedJournal(500m, LedgerFixture.InPriorYear), "alice");
        await f.Journals.SubmitForReviewAsync(created.Id, "alice");
        await f.Journals.ReviewAsync(created.Id, "bob");
        var posted = await f.Journals.ApproveAndPostAsync(created.Id, "carol");
        posted.EntryNo.Should().Contain("2025");

        var reversal = await f.Journals.ReverseAsync(posted.Id, "dave");

        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == reversal.Id);
        // JV-<year>-nnnn. Numbering a 2026 reversal into the 2025 sequence would collide with 2025's
        // own numbering and misfile the entry in every year-scoped report.
        entry.EntryNo.Should().Contain(DateTime.UtcNow.Year.ToString());
        entry.EntryNo.Should().NotContain("2025");
        entry.PeriodId.Should().Be(LedgerFixture.CurrentPeriodId);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static async Task<JournalReadDto> PostAsync(LedgerFixture f, decimal amount = 500m)
    {
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(amount), "alice");
        await f.Journals.SubmitForReviewAsync(created.Id, "alice");
        await f.Journals.ReviewAsync(created.Id, "bob");
        return await f.Journals.ApproveAndPostAsync(created.Id, "carol");
    }

    private static async Task SetCurrentPeriodStatusAsync(LedgerFixture f, PeriodStatus status)
    {
        var p = await f.Db.AccountingPeriods.SingleAsync(x => x.Id == LedgerFixture.CurrentPeriodId);
        p.Status = status;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
    }
}
