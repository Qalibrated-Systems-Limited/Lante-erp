using FinanceService.Core.DTOs;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Supplier invoice cancellation — the AP mirror of <see cref="InvoiceCancellationTests"/> (#332).
///
/// <para><c>SupplierInvoiceStatus.Cancelled</c> was read at three sites — <c>SetMatchStatusAsync</c>,
/// <c>InputVatForPeriodAsync</c> and <c>CashFlowService</c> — and assigned by nothing, so a bill
/// entered in error stayed an expense and a payable for ever, and its input VAT stayed claimable.</para>
///
/// <para><b>The AP side makes the PaidAmount guard's case more plainly than AR did.</b>
/// <c>PaymentVoucherService</c> sets Paid/PartPaid from the balance when a voucher pays a bill, exactly
/// as <c>ReceiptService</c> does for invoices. Two separate services now maintain that status as a side
/// effect of moving money, and neither exists to protect this method. The money is the fact; the status
/// is a derived summary. So the guard reads the money.</para>
/// </summary>
public class SupplierInvoiceCancellationTests
{
    // ── The happy paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_received_bill_cancels_with_no_journal_at_all()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");

        var cancelled = await f.Bills.CancelAsync(created.Id, "Supplier billed the wrong entity", "carol");

        cancelled.Status.Should().Be(nameof(SupplierInvoiceStatus.Cancelled));
        // Received is the pre-approval state and posts nothing.
        (await f.Db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancelling_an_approved_bill_reverses_its_posting()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        await f.Bills.CancelAsync(created.Id, "Duplicate of BILL-2026-0001", "carol");

        var original = await f.Db.JournalEntries.SingleAsync(e => e.SourceDocumentId == created.Id);
        original.Status.Should().Be(JournalStatus.Reversed);
        (await f.Db.JournalEntries.CountAsync(e => e.ReversalOfId == original.Id)).Should().Be(1);
    }

    [Fact]
    public async Task The_expense_the_input_VAT_and_the_payable_all_come_back_out()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        await f.Bills.CancelAsync(created.Id, "Entered in error", "carol");

        var rows = await f.Db.GeneralLedgerEntries.ToListAsync();
        foreach (var acct in rows.GroupBy(g => g.AccountId))
            acct.Sum(g => g.Debit - g.Credit).Should()
                .Be(0m, $"account {acct.Key} should net to zero once the bill is cancelled");
    }

    [Fact]
    public async Task A_cancelled_bill_stops_being_claimable_input_VAT()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");
        var start = LedgerFixture.InPeriod.AddDays(-20);
        var end = LedgerFixture.InPeriod.AddDays(20);
        (await f.Bills.InputVatForPeriodAsync(start, end)).Should().Be(160m);

        await f.Bills.CancelAsync(created.Id, "Entered in error", "carol");

        // One of the three filters that read Cancelled and could never fire. This one has a direct
        // consequence: input VAT on a bill that was never really incurred would be reclaimed from KRA.
        (await f.Bills.InputVatForPeriodAsync(start, end)).Should().Be(0m);
    }

    [Fact]
    public async Task A_cancelled_bill_owes_nothing_and_records_why()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        var cancelled = await f.Bills.CancelAsync(created.Id, "  Goods never delivered  ", "carol");

        cancelled.Balance.Should().Be(0m);
        cancelled.CancellationReason.Should().Be("Goods never delivered");
        cancelled.CancelledAt.Should().NotBeNull();
    }

    // ── Money already paid ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_bill_already_paid_by_a_voucher_cannot_be_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");
        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = created.Id }, "alice");
        await f.Vouchers.ApproveAsync(voucher.Id, "bob");
        await f.Vouchers.PayAsync(voucher.Id, "alice");

        var act = () => f.Bills.CancelAsync(created.Id, "changed my mind", "carol");

        // The voucher posted its own journal and the cash has left the bank. Cancelling would reverse
        // the expense and the payable while the money stays gone.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*paid against it*");
    }

    [Fact]
    public async Task A_stale_status_does_not_let_a_paid_bill_be_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        // Money paid, status NOT moved off Approved — what a second payment path that forgets
        // PaymentVoucherService's status line would leave behind.
        var row = await f.Db.SupplierInvoices.SingleAsync(b => b.Id == created.Id);
        row.PaidAmount = 300m;
        row.Balance -= 300m;
        row.Status = SupplierInvoiceStatus.Approved;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var act = () => f.Bills.CancelAsync(created.Id, "cleanup", "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*paid against it*");
    }

    // ── State guards ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_bill_cannot_be_cancelled_twice()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");
        await f.Bills.CancelAsync(created.Id, "Entered in error", "carol");

        var act = () => f.Bills.CancelAsync(created.Id, "again", "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already cancelled*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_cancellation_reason_is_required(string reason)
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");

        var act = () => f.Bills.CancelAsync(created.Id, reason, "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reason is required*");
    }

    [Fact]
    public async Task Cancelling_an_unknown_bill_is_a_not_found_rather_than_a_silent_no_op()
    {
        using var f = new LedgerFixture();

        var act = () => f.Bills.CancelAsync("does-not-exist", "Entered in error", "carol");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task An_approved_bill_with_no_journal_recorded_is_refused_rather_than_half_cancelled()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        var row = await f.Db.SupplierInvoices.SingleAsync(b => b.Id == created.Id);
        row.JournalEntryId = null;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var act = () => f.Bills.CancelAsync(created.Id, "cleanup", "carol");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*needs investigation*");
    }

    // ── The period lock reaches through cancel ──────────────────────────────────

    [Fact]
    public async Task Cancelling_is_refused_when_the_reversal_cannot_be_posted()
    {
        using var f = new LedgerFixture(currentPeriodStatus: PeriodStatus.Closed);
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");

        var act = () => f.Bills.CancelAsync(created.Id, "Entered in error", "carol");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Closed*");
    }

    [Fact]
    public async Task A_refused_cancellation_leaves_the_bill_exactly_as_it_was()
    {
        using var f = new LedgerFixture(currentPeriodStatus: PeriodStatus.Closed);
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        var approved = await f.Bills.ApproveAsync(created.Id, "bob");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Bills.CancelAsync(created.Id, "Entered in error", "carol"));
        f.Db.ChangeTracker.Clear();

        var row = await f.Db.SupplierInvoices.SingleAsync(b => b.Id == created.Id);
        row.Status.Should().Be(SupplierInvoiceStatus.Approved);
        row.Balance.Should().Be(approved.Total);
        row.CancellationReason.Should().BeNull();
        row.CancelledAt.Should().BeNull();
    }

    // ── Interaction with the 3-way match ────────────────────────────────────────

    [Fact]
    public async Task The_three_way_match_cannot_be_rewritten_on_a_cancelled_bill()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1000m), "alice");
        await f.Bills.ApproveAsync(created.Id, "bob");
        await f.Bills.CancelAsync(created.Id, "Entered in error", "carol");

        var act = () => f.Bills.SetMatchStatusAsync(created.Id, ThreeWayMatchStatus.Matched, "late", "procurement");

        // SetMatchStatusAsync already refused Paid or Cancelled bills — the Cancelled half of that
        // guard had never been reachable. Procurement's write-back seam calls this, so a match arriving
        // after a cancellation must not quietly reopen the bill's story.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
