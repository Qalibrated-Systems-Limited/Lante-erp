using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Invoice cancellation (#332).
///
/// <para><b>Why cancel and not delete.</b> An issued invoice must never vanish: the customer holds a
/// copy, the GL holds a posting, and the AR subledger holds a balance. Removing the row leaves the
/// trial balance and the statement disagreeing with nothing to reconcile them. <c>InvoiceStatus.Cancelled</c>
/// already existed for exactly this — read by seven filters across VatService, CashFlowService and
/// AgingAsync, and assigned by nothing at all. These tests are what make it reachable.</para>
///
/// <para><b>The guard worth reading twice</b> is <c>PaidAmount &gt; 0</c> rather than a status check
/// alone. <c>ReceiptService</c> does set Paid/PartPaid whenever it allocates, so status would be the
/// tidier test — but that is one code path maintaining two facts, and the fact that matters is the
/// money. <c>A_stale_status_does_not_let_a_paid_invoice_be_cancelled</c> is the test for the version of
/// this that would otherwise ship the day a second allocation path is added.</para>
/// </summary>
public class InvoiceCancellationTests
{
    // ── The happy paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_invoice_cancels_with_no_journal_at_all()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        var cancelled = await f.Invoices.CancelAsync(created.Id, "Raised against the wrong customer", "carol");

        cancelled.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        // A draft never posted, so there is nothing to reverse. Reversing here would go looking for a
        // journal that does not exist and fail on a perfectly ordinary cancellation.
        (await f.Db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancelling_an_issued_invoice_reverses_its_posting()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Invoices.CancelAsync(created.Id, "Duplicate of INV-2026-0001", "carol");

        var original = await f.Db.JournalEntries.SingleAsync(e => e.SourceDocumentId == created.Id);
        original.Status.Should().Be(JournalStatus.Reversed);
        (await f.Db.JournalEntries.CountAsync(e => e.ReversalOfId == original.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Every_account_the_invoice_touched_nets_back_to_zero()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        // The point of reversing rather than deleting: revenue, VAT and the receivable all come back
        // out, and the audit trail of both postings stays.
        var rows = await f.Db.GeneralLedgerEntries.ToListAsync();
        foreach (var acct in rows.GroupBy(g => g.AccountId))
            acct.Sum(g => g.Debit - g.Credit).Should()
                .Be(0m, $"account {acct.Key} should net to zero once the invoice is cancelled");
    }

    [Fact]
    public async Task A_cancelled_invoice_owes_nothing_and_records_why()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        var cancelled = await f.Invoices.CancelAsync(created.Id, "  Customer withdrew the order  ", "carol");

        cancelled.Balance.Should().Be(0m);
        // Trimmed, and carried back out to the caller — "cancelled" with no stated reason is the case
        // that becomes unanswerable months later.
        cancelled.CancellationReason.Should().Be("Customer withdrew the order");
        cancelled.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_cancelled_invoice_drops_out_of_the_aging_report()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");
        (await f.Invoices.AgingAsync(LedgerFixture.InPeriod.AddDays(60))).Should().NotBeEmpty();

        await f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        // One of the seven filters that read Cancelled and could never fire. Asserted through the
        // report rather than the flag, because the report is what someone chases the debt from.
        var aging = await f.Invoices.AgingAsync(LedgerFixture.InPeriod.AddDays(60));
        aging.Sum(r => r.Days31To60 + r.Days1To30 + r.Current).Should().Be(0m);
    }

    // ── Money already received ──────────────────────────────────────────────────

    [Fact]
    public async Task An_invoice_with_a_part_payment_cannot_be_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");
        await f.Receipts.CreateAsync(LedgerFixture.Receipt(400m), "bob");

        var act = () => f.Invoices.CancelAsync(created.Id, "changed my mind", "carol");

        // The receipt posted its own journal and the cash is in the bank. Cancelling would reverse the
        // receivable while leaving the money, so the two sides stop agreeing.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*received against it*");
    }

    [Fact]
    public async Task A_stale_status_does_not_let_a_paid_invoice_be_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        // Money allocated, status NOT moved off Issued — what a second allocation path that forgets
        // ReceiptService's status line would leave behind. The whole reason the guard reads PaidAmount.
        var row = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        row.PaidAmount = 250m;
        row.Balance -= 250m;
        row.Status = InvoiceStatus.Issued;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var act = () => f.Invoices.CancelAsync(created.Id, "cleanup", "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*received against it*");
    }

    // ── State guards ────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_invoice_cannot_be_cancelled_twice()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");
        await f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        var act = () => f.Invoices.CancelAsync(created.Id, "again", "carol");

        // Double-cancelling would post a second reversal, turning a corrected invoice into a credit.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already cancelled*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_cancellation_reason_is_required(string reason)
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        var act = () => f.Invoices.CancelAsync(created.Id, reason, "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reason is required*");
    }

    [Fact]
    public async Task Cancelling_an_unknown_invoice_is_a_not_found_rather_than_a_silent_no_op()
    {
        using var f = new LedgerFixture();

        var act = () => f.Invoices.CancelAsync("does-not-exist", "Billed in error", "carol");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task An_issued_invoice_with_no_journal_recorded_is_refused_rather_than_half_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        // IssueAsync writes JournalEntryId and the status in one SaveChanges, so this pairing should
        // hold — but if it ever does not, cancelling would leave the GL posting standing behind a
        // Cancelled invoice, which is the worst of both states.
        var row = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        row.JournalEntryId = null;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var act = () => f.Invoices.CancelAsync(created.Id, "cleanup", "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*needs investigation*");
    }

    // ── The period lock reaches through cancel ──────────────────────────────────

    [Fact]
    public async Task Cancelling_is_refused_when_the_reversal_cannot_be_posted()
    {
        using var f = new LedgerFixture(currentPeriodStatus: PeriodStatus.Closed);
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        var act = () => f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        // #342: the reversal resolves the period covering its own date and refuses if it is closed.
        // Cancel does not catch that — a cancelled invoice whose revenue is still recognised is worse
        // than a cancellation that failed and said so.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Closed*");
    }

    [Fact]
    public async Task A_refused_cancellation_leaves_the_invoice_exactly_as_it_was()
    {
        using var f = new LedgerFixture(currentPeriodStatus: PeriodStatus.Closed);
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Invoices.CancelAsync(created.Id, "Billed in error", "carol"));
        f.Db.ChangeTracker.Clear();

        var row = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        row.Status.Should().Be(InvoiceStatus.Issued);
        row.Balance.Should().Be(issued.Total);
        row.CancellationReason.Should().BeNull();
        row.CancelledAt.Should().BeNull();
    }

    // ── eTIMS ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_after_eTIMS_acceptance_records_that_a_credit_note_is_still_owed()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");
        (await f.Db.Invoices.SingleAsync(i => i.Id == created.Id)).EtimsStatus
            .Should().Be(EtimsStatus.Accepted);

        var cancelled = await f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        // KRA has been told this invoice exists; cancelling it locally does not untell them. The
        // provider is a stub (#224), so recording the debt is the only honest thing available —
        // flipping EtimsStatus would claim a submission that never happened.
        cancelled.Notes.Should().Contain("credit note");
        cancelled.Notes.Should().Contain("#224");
    }

    [Fact]
    public async Task The_eTIMS_status_is_not_quietly_rewritten_on_cancel()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        var row = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        row.EtimsStatus.Should().Be(EtimsStatus.Accepted);
    }
}
