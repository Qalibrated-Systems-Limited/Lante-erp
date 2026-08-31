using FinanceService.Core.DTOs;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Accounts-payable posting, and the three-way match control that gates it.
///
/// <para>
/// AP is the mirror of AR and gets the same treatment, but it carries one control AR does not:
/// an invoice whose PO/GRN/invoice match has failed must not be approved. That is the check
/// standing between the company and paying for goods it never received, so it gets tested
/// explicitly rather than assumed.
/// </para>
/// </summary>
public class SupplierInvoicePostingTests
{
    private const string PayablesCode = "2100";
    private const string InputVatCode = "1230";
    private const string CostOfSalesCode = "5100";

    // ── The posting balances, mirrored from AR ───────────────────────────────────

    [Fact]
    public async Task Approving_a_bill_posts_a_balanced_journal()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");

        await f.Bills.ApproveAsync(created.Id, "bob");

        var entry = await SourceJournalAsync(f, created.Id);
        entry.TotalDebit.Should().Be(entry.TotalCredit);
    }

    [Fact]
    public async Task Payables_is_credited_with_the_gross_and_expense_debited_net()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m, taxCode: "A"), "alice");

        var approved = await f.Bills.ApproveAsync(created.Id, "bob");

        var lines = await SourceJournalLinesAsync(f, created.Id);
        var payable = lines.Single(l => AccountCodeOf(f, l.AccountId) == PayablesCode);
        var expense = lines.Single(l => AccountCodeOf(f, l.AccountId) == CostOfSalesCode);
        var inputVat = lines.Single(l => AccountCodeOf(f, l.AccountId) == InputVatCode);

        // We owe the supplier the gross; the expense is the net; the VAT is recoverable, an
        // asset rather than a cost. Folding VAT into the expense overstates costs and loses
        // the reclaim.
        payable.Credit.Should().Be(approved.Total);
        expense.Debit.Should().Be(1000m);
        inputVat.Debit.Should().Be(160m);
        (expense.Debit + inputVat.Debit).Should().Be(payable.Credit);
    }

    [Fact]
    public async Task An_exempt_bill_posts_no_input_VAT_line()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m, taxCode: "E"), "alice");

        await f.Bills.ApproveAsync(created.Id, "bob");

        var lines = await SourceJournalLinesAsync(f, created.Id);
        lines.Should().NotContain(l => AccountCodeOf(f, l.AccountId) == InputVatCode);
        lines.Should().HaveCount(2);
    }

    // ── The three-way match control ──────────────────────────────────────────────

    [Fact]
    public async Task A_bill_with_a_match_exception_cannot_be_approved()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");
        await f.Bills.SetMatchStatusAsync(created.Id, ThreeWayMatchStatus.Exception, "qty differs", "alice");

        var act = () => f.Bills.ApproveAsync(created.Id, "bob");

        // This is the control that stops the company paying for goods it never received.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*3-way match*");
    }

    [Theory]
    [InlineData(ThreeWayMatchStatus.Matched)]
    [InlineData(ThreeWayMatchStatus.NotRequired)]
    public async Task A_bill_that_matched_or_needs_no_match_approves(ThreeWayMatchStatus status)
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");
        await f.Bills.SetMatchStatusAsync(created.Id, status, null, "alice");

        var approved = await f.Bills.ApproveAsync(created.Id, "bob");

        // The read DTO stringly-types Status, so compare against the enum name.
        approved.Status.Should().Be(nameof(SupplierInvoiceStatus.Approved));
    }

    [Fact]
    public async Task The_match_result_is_frozen_once_the_bill_is_paid_or_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");
        var bill = await f.Db.SupplierInvoices.SingleAsync(b => b.Id == created.Id);
        bill.Status = SupplierInvoiceStatus.Paid;
        await f.Db.SaveChangesAsync();

        var act = () => f.Bills.SetMatchStatusAsync(created.Id, ThreeWayMatchStatus.Exception, "too late", "alice");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*can no longer change*");
    }

    /// <summary>
    /// Resolved by #229 (option C): an Approved bill can still have its match result rewritten —
    /// Procurement owns the match, and a GRN filed late or a correction arriving after approval
    /// should stay recordable, rather than stranding the record or refusing a legitimate update.
    ///
    /// <para>
    /// But it is no longer silent. The bill still carries a posted GL liability, so flipping the
    /// match to Exception after approval now flags it (<c>MatchExceptionFlaggedAt</c>) and — per
    /// <see cref="PaymentVoucherService.CreateAsync"/> — refuses a new payment voucher against it
    /// until someone reviews it. This assertion previously pinned the pre-fix behaviour ("nothing
    /// surfaces the contradiction"); it now pins the opposite, deliberately.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_approved_bill_accepts_a_changed_match_result_but_it_is_flagged_and_blocks_payment()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        var updated = await f.Bills.SetMatchStatusAsync(
            created.Id, ThreeWayMatchStatus.Exception, "GRN arrived late, quantities differ", "alice");

        updated.MatchStatus.Should().Be(nameof(ThreeWayMatchStatus.Exception));
        updated.MatchExceptionFlaggedAt.Should().NotBeNull();
        // The posted journal is untouched — Procurement's correction doesn't reverse money on its
        // own — but the bill is now refused for payment until the flag is resolved.
        var entry = await SourceJournalAsync(f, created.Id);
        entry.Status.Should().Be(JournalStatus.Posted);

        var act = () => f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = created.Id }, "carol");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unresolved 3-way match exception*");
    }

    [Fact]
    public async Task Resolving_the_match_off_exception_clears_the_flag_and_unblocks_payment()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");
        await f.Bills.SetMatchStatusAsync(created.Id, ThreeWayMatchStatus.Exception, "qty differs", "alice");

        var resolved = await f.Bills.SetMatchStatusAsync(created.Id, ThreeWayMatchStatus.Matched, "GRN corrected, now matches", "alice");

        resolved.MatchExceptionFlaggedAt.Should().BeNull();
        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = created.Id }, "carol");
        voucher.Should().NotBeNull();
    }

    [Fact]
    public async Task A_normal_pre_approval_match_update_is_not_flagged()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");

        var updated = await f.Bills.SetMatchStatusAsync(created.Id, ThreeWayMatchStatus.Exception, "qty differs", "alice");

        updated.MatchExceptionFlaggedAt.Should().BeNull();
    }

    // ── State guards ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_bill_cannot_be_approved_twice()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        var act = () => f.Bills.ApproveAsync(created.Id, "bob");

        // Double approval would post the liability twice — the company would appear to owe
        // double what it does.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only Received invoices*");
    }

    [Fact]
    public async Task A_bill_needs_at_least_one_line_and_a_real_supplier()
    {
        using var f = new LedgerFixture();

        var noLines = LedgerFixture.Bill();
        noLines.Lines.Clear();
        await FluentActions.Awaiting(() => f.Bills.CreateAsync(noLines, "alice"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*at least one line*");

        var noSupplier = LedgerFixture.Bill();
        noSupplier.SupplierId = "nobody";
        await FluentActions.Awaiting(() => f.Bills.CreateAsync(noSupplier, "alice"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Supplier not found*");
    }

    [Fact]
    public async Task Approving_an_unknown_bill_is_a_not_found()
    {
        using var f = new LedgerFixture();

        var act = () => f.Bills.ApproveAsync("does-not-exist", "bob");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Period interaction and linkage ───────────────────────────────────────────

    [Fact]
    public async Task Approving_into_a_locked_period_is_refused_by_the_ledger_underneath()
    {
        using var f = new LedgerFixture(periodStatus: PeriodStatus.Locked);
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");

        var act = () => f.Bills.ApproveAsync(created.Id, "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Locked*");
    }

    [Fact]
    public async Task The_journal_records_which_bill_produced_it()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(), "alice");

        await f.Bills.ApproveAsync(created.Id, "bob");

        var entry = await SourceJournalAsync(f, created.Id);
        entry.SourceDocumentId.Should().Be(created.Id);
        entry.Status.Should().Be(JournalStatus.Posted);
    }

    // ── AR and AP together ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_sale_and_a_purchase_in_the_same_period_leave_the_trial_balance_balanced()
    {
        using var f = new LedgerFixture();

        var invoice = await f.Invoices.CreateAsync(LedgerFixture.Invoice(5000m), "alice");
        await f.Invoices.IssueAsync(invoice.Id, "alice");

        var bill = await f.Bills.CreateAsync(LedgerFixture.Bill(2000m), "alice");
        await f.Bills.ApproveAsync(bill.Id, "bob");

        var tb = await f.Journals.GetTrialBalanceAsync(new DateTime(2026, 6, 30), null, null);

        // The end-to-end assertion: two subledgers posting independently still produce one
        // balanced ledger.
        tb.IsBalanced.Should().BeTrue();
        tb.TotalDebit.Should().Be(tb.TotalCredit);
    }

    [Fact]
    public async Task Input_VAT_for_the_period_reflects_approved_bills_only()
    {
        using var f = new LedgerFixture();

        var approved = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(approved.Id, "bob");

        // Created but deliberately left unapproved.
        await f.Bills.CreateAsync(LedgerFixture.Bill(9999m), "alice");

        var inputVat = await f.Bills.InputVatForPeriodAsync(
            new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));

        // Reclaiming VAT on a bill nobody approved would be a filing error, not a rounding one.
        inputVat.Should().Be(160m);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static async Task<Core.Entities.JournalEntry> SourceJournalAsync(LedgerFixture f, string billId) =>
        await f.Db.JournalEntries.SingleAsync(e => e.SourceDocumentId == billId);

    private static async Task<List<Core.Entities.JournalLine>> SourceJournalLinesAsync(LedgerFixture f, string billId)
    {
        var entry = await SourceJournalAsync(f, billId);
        return await f.Db.JournalLines.Where(l => l.JournalEntryId == entry.Id).ToListAsync();
    }

    private static string AccountCodeOf(LedgerFixture f, string accountId) =>
        f.Db.ChartOfAccounts.Single(a => a.Id == accountId).Code;
}
