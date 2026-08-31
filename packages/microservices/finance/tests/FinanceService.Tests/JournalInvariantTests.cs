using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Double-entry invariants for the general ledger.
///
/// <para>
/// finance had no tests at all before this, while being the service that writes journal entries
/// — the one place in the platform where a defect is least recoverable, because wrong numbers in
/// a customer's books cannot be quietly corrected later. The rules below were already enforced by
/// <c>JournalService</c>; nothing verified they stayed enforced.
/// </para>
/// <para>
/// The invariant set is ported from QaliSuite's ledger suite (see issue #212 for the extraction).
/// The assertions transfer; none of the implementation does — different language, different
/// database, different domain model.
/// </para>
/// </summary>
public class JournalInvariantTests
{
    // ── The core four ────────────────────────────────────────────────────────────
    // If only these four survive a future refactor, the ledger is still trustworthy.

    [Fact]
    public async Task A_posted_journal_has_debits_equal_to_credits()
    {
        using var f = new LedgerFixture();

        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(1234.56m), "preparer");

        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == created.Id);
        entry.TotalDebit.Should().Be(entry.TotalCredit);
        entry.TotalDebit.Should().Be(1234.56m);
    }

    [Fact]
    public async Task An_unbalanced_journal_is_refused()
    {
        using var f = new LedgerFixture();

        var dto = LedgerFixture.BalancedJournal();
        dto.Lines[1].Credit = 999m;   // debit 1000 vs credit 999

        var act = () => f.Journals.CreateAsync(dto, "preparer");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unbalanced*");
    }

    [Fact]
    public async Task A_journal_needs_at_least_two_lines()
    {
        using var f = new LedgerFixture();

        var dto = LedgerFixture.BalancedJournal();
        dto.Lines.RemoveAt(1);

        var act = () => f.Journals.CreateAsync(dto, "preparer");

        // A one-sided entry is the classic way a ledger silently stops balancing.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least two lines*");
    }

    [Fact]
    public async Task Every_line_carries_a_debit_or_a_credit_but_never_both()
    {
        using var f = new LedgerFixture();

        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "preparer");

        var lines = await f.Db.JournalLines.Where(l => l.JournalEntryId == created.Id).ToListAsync();
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
        lines.Should().OnlyContain(l => (l.Debit > 0m) ^ (l.Credit > 0m),
            "a line with both a debit and a credit, or neither, is not a double-entry line");
    }

    // ── Amount guards ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_zero_value_journal_is_refused()
    {
        using var f = new LedgerFixture();

        // Balanced, two lines, and completely meaningless.
        var act = () => f.Journals.CreateAsync(LedgerFixture.BalancedJournal(0m), "preparer");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*greater than zero*");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1234.56)]
    [InlineData(999999.99)]
    public async Task Amounts_survive_the_round_trip_without_drift(decimal amount)
    {
        using var f = new LedgerFixture();

        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(amount), "preparer");

        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == created.Id);
        // decimal, not double — this asserts the type choice as much as the value.
        entry.TotalDebit.Should().Be(amount);
        entry.TotalCredit.Should().Be(amount);
    }

    // ── Period locks ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PeriodStatus.Closed)]
    [InlineData(PeriodStatus.Locked)]
    public async Task Posting_into_a_non_open_period_is_refused(PeriodStatus status)
    {
        using var f = new LedgerFixture(periodStatus: status);

        var act = () => f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "preparer");

        // Whole point of closing a period: the reported numbers stop moving.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{status}*");
    }

    [Fact]
    public async Task Posting_to_a_date_no_period_covers_is_refused()
    {
        using var f = new LedgerFixture();

        var act = () => f.Journals.CreateAsync(
            LedgerFixture.BalancedJournal(date: LedgerFixture.OutsideAnyPeriod), "preparer");

        // Refusing beats silently inventing a period — an entry with no period cannot be closed.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No accounting period*");
    }

    [Fact]
    public async Task An_open_period_still_accepts_postings_when_it_is_the_covering_one()
    {
        using var f = new LedgerFixture(periodStatus: PeriodStatus.Open);

        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "preparer");

        created.Should().NotBeNull();
    }

    // ── Chart-of-accounts integrity ──────────────────────────────────────────────

    [Fact]
    public async Task Posting_to_a_header_account_is_refused()
    {
        using var f = new LedgerFixture();

        var dto = LedgerFixture.BalancedJournal();
        dto.Lines[0].AccountId = LedgerFixture.HeaderAccountId;

        var act = () => f.Journals.CreateAsync(dto, "preparer");

        // Header accounts aggregate their children. Posting to one double-counts on every report.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*header account*");
    }

    // ── Segregation of duties ────────────────────────────────────────────────────

    [Fact]
    public async Task The_reviewer_must_not_be_the_preparer()
    {
        using var f = new LedgerFixture();
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "alice");
        await f.Journals.SubmitForReviewAsync(created.Id, "alice");

        var act = () => f.Journals.ReviewAsync(created.Id, "alice");

        // The control exists so one person cannot move money end to end unobserved.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*segregation of duties*");
    }

    [Fact]
    public async Task The_approver_must_differ_from_the_preparer_and_the_reviewer()
    {
        using var f = new LedgerFixture();
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "alice");
        await f.Journals.SubmitForReviewAsync(created.Id, "alice");
        await f.Journals.ReviewAsync(created.Id, "bob");

        var asPreparer = () => f.Journals.ApproveAndPostAsync(created.Id, "alice");
        await asPreparer.Should().ThrowAsync<InvalidOperationException>();

        var asReviewer = () => f.Journals.ApproveAndPostAsync(created.Id, "bob");
        await asReviewer.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Three_distinct_people_can_carry_an_entry_through_to_posted()
    {
        using var f = new LedgerFixture();
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "alice");
        await f.Journals.SubmitForReviewAsync(created.Id, "alice");
        await f.Journals.ReviewAsync(created.Id, "bob");

        var posted = await f.Journals.ApproveAndPostAsync(created.Id, "carol");

        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == posted.Id);
        entry.Status.Should().Be(JournalStatus.Posted);
        entry.PostedAt.Should().NotBeNull();
    }

    // ── Reversal ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unposted_journal_cannot_be_reversed()
    {
        using var f = new LedgerFixture();
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "alice");

        var act = () => f.Journals.ReverseAsync(created.Id, "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only posted journals*");
    }

    [Fact]
    public async Task A_reversal_inverts_the_original_and_the_pair_nets_to_zero()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f, 500m);

        var reversal = await f.Journals.ReverseAsync(posted.Id, "carol");

        var original = await f.Db.JournalEntries.SingleAsync(e => e.Id == posted.Id);
        var reversed = await f.Db.JournalEntries.SingleAsync(e => e.Id == reversal.Id);

        // Direction is inverted...
        reversed.TotalDebit.Should().Be(original.TotalCredit);
        reversed.TotalCredit.Should().Be(original.TotalDebit);
        reversed.IsReversal.Should().BeTrue();
        reversed.ReversalOfId.Should().Be(original.Id);

        // ...and the two together move nothing.
        (original.TotalDebit - reversed.TotalDebit).Should().Be(0m);
        (original.TotalCredit - reversed.TotalCredit).Should().Be(0m);
    }

    [Fact]
    public async Task A_reversal_leaves_every_account_net_zero_line_by_line()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f, 750m);
        var reversal = await f.Journals.ReverseAsync(posted.Id, "carol");

        var lines = await f.Db.JournalLines
            .Where(l => l.JournalEntryId == posted.Id || l.JournalEntryId == reversal.Id)
            .ToListAsync();

        // The assertion that actually matters: not just that totals cancel, but that they cancel
        // per account. Totals can balance while the money has moved between accounts.
        foreach (var group in lines.GroupBy(l => l.AccountId))
        {
            var net = group.Sum(l => l.Debit) - group.Sum(l => l.Credit);
            net.Should().Be(0m, $"account {group.Key} should be untouched once reversed");
        }
    }

    [Fact]
    public async Task The_original_entry_survives_a_reversal()
    {
        using var f = new LedgerFixture();
        var posted = await PostAsync(f, 300m);

        await f.Journals.ReverseAsync(posted.Id, "carol");

        // An audit trail that deletes the mistake is not an audit trail.
        var original = await f.Db.JournalEntries.SingleAsync(e => e.Id == posted.Id);
        original.Should().NotBeNull();
        original.TotalDebit.Should().Be(300m);
    }

    // ── Trial balance ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_trial_balance_balances_after_several_postings()
    {
        using var f = new LedgerFixture();
        await PostAsync(f, 100m);
        await PostAsync(f, 250.75m);
        await PostAsync(f, 1000m);

        var tb = await f.Journals.GetTrialBalanceAsync(new DateTime(2026, 6, 30), null, null);

        tb.IsBalanced.Should().BeTrue();
        tb.TotalDebit.Should().Be(tb.TotalCredit);
    }

    // ── Configuration guards ─────────────────────────────────────────────────────

    [Fact]
    public async Task Posting_without_a_configured_base_currency_is_refused()
    {
        using var f = new LedgerFixture(withBaseCurrency: false);

        var act = () => f.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "preparer");

        // Failing loudly beats defaulting to a rate of 1 and silently mis-stating the books.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*base currency*");
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    private static async Task<JournalReadDto> PostAsync(LedgerFixture f, decimal amount)
    {
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(amount), "alice");
        await f.Journals.SubmitForReviewAsync(created.Id, "alice");
        await f.Journals.ReviewAsync(created.Id, "bob");
        return await f.Journals.ApproveAndPostAsync(created.Id, "carol");
    }
}
