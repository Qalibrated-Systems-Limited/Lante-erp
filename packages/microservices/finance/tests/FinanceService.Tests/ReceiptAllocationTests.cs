using FinanceService.Core.DTOs;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Where a receipt's money lands (#212).
///
/// <para>Payment allocation had no tests at all, which is how the defect below survived: the
/// <b>auto</b> allocation path filters out Draft and Cancelled invoices, and the <b>explicit</b> path
/// — the one the UI uses when a user ticks specific invoices — reaches the same write with no such
/// check. One correct filter, applied to one of the two paths into it. Exactly the shape of #342,
/// where the period lock guarded <c>CreateAsync</c> and not <c>ReverseAsync</c>.</para>
///
/// <para>Measured before fixing, on a 500 allocation to an unissued 1,160 invoice:
/// the invoice flipped to PartPaid; <c>IssueAsync</c> then refused it for ever ("only Draft invoices
/// can be issued"), so its revenue became permanently unrecognisable; and the receivable control
/// account moved <b>-500</b> — credited by the receipt with nothing ever having debited it.</para>
/// </summary>
public class ReceiptAllocationTests
{
    private static CreateReceiptDto ReceiptFor(string invoiceId, decimal amount) 
    {
        var dto = LedgerFixture.Receipt(amount);
        dto.Allocations.Add(new AllocationDto { InvoiceId = invoiceId, Amount = amount });
        return dto;
    }

    // ── The defect ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_explicit_allocation_to_a_draft_invoice_is_refused()
    {
        using var f = new LedgerFixture();
        var draft = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        var act = () => f.Receipts.CreateAsync(ReceiptFor(draft.Id, 500m), "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Issue it first*");
    }

    [Fact]
    public async Task A_refused_allocation_leaves_the_draft_issuable()
    {
        using var f = new LedgerFixture();
        var draft = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Receipts.CreateAsync(ReceiptFor(draft.Id, 500m), "bob"));
        f.Db.ChangeTracker.Clear();

        // The consequence that made this worth failing loudly rather than skipping: once the invoice
        // left Draft it could never be issued, so the revenue was unrecognisable for good.
        var issued = await f.Invoices.IssueAsync(draft.Id, "alice");
        issued.Status.Should().Be(nameof(InvoiceStatus.Issued));
    }

    [Fact]
    public async Task A_refused_allocation_banks_no_cash_and_moves_no_receivable()
    {
        using var f = new LedgerFixture();
        var draft = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Receipts.CreateAsync(ReceiptFor(draft.Id, 500m), "bob"));
        f.Db.ChangeTracker.Clear();

        // Before the fix this left the receivable control at -500: the receipt credited it while
        // nothing had ever debited it, because the invoice was never posted.
        (await f.Db.Payments.CountAsync()).Should().Be(0);
        (await f.Db.GeneralLedgerEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_explicit_allocation_to_a_cancelled_invoice_is_refused()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");
        await f.Invoices.CancelAsync(created.Id, "Billed in error", "carol");

        var act = () => f.Receipts.CreateAsync(ReceiptFor(created.Id, 500m), "bob");

        // Previously this was skipped rather than refused, and only by accident: cancelling sets
        // Balance to 0, so Math.Min clamped the allocation to nothing. Protection by arithmetic
        // rather than by a rule — and the arithmetic belongs to a different feature.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cancelled invoice is owed nothing*");
    }

    // ── The paths that must keep working ────────────────────────────────────────

    [Fact]
    public async Task An_explicit_allocation_to_an_issued_invoice_settles_it()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Receipts.CreateAsync(ReceiptFor(created.Id, issued.Total), "bob");

        var after = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        after.Status.Should().Be(InvoiceStatus.Paid);
        after.PaidAmount.Should().Be(issued.Total);
        after.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task A_part_payment_leaves_the_invoice_part_paid()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Receipts.CreateAsync(ReceiptFor(created.Id, 400m), "bob");

        var after = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        after.Status.Should().Be(InvoiceStatus.PartPaid);
        after.Balance.Should().Be(issued.Total - 400m);
    }

    [Fact]
    public async Task Successive_receipts_accumulate_rather_than_overwrite()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Receipts.CreateAsync(ReceiptFor(created.Id, 400m), "bob");
        await f.Receipts.CreateAsync(ReceiptFor(created.Id, 300m), "bob");

        var after = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        after.PaidAmount.Should().Be(700m);
        after.Balance.Should().Be(issued.Total - 700m);
    }

    [Fact]
    public async Task Auto_allocation_still_skips_a_draft_rather_than_throwing()
    {
        using var f = new LedgerFixture();
        var issuedInv = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        await f.Invoices.IssueAsync(issuedInv.Id, "alice");
        await f.Invoices.CreateAsync(LedgerFixture.Invoice(500m), "alice");   // left Draft

        // 1,500 deliberately EXCEEDS the issued invoice's 1,160 balance, so auto-allocation runs out
        // of legitimate invoices and would reach the Draft if its query stopped excluding one. A
        // smaller receipt is absorbed entirely by the first invoice and never exercises the filter at
        // all — which is exactly how the first version of this test passed against a mutant that
        // deleted it.
        var receipt = await f.Receipts.CreateAsync(LedgerFixture.Receipt(1500m), "bob");

        receipt.Should().NotBeNull();
        // The surplus stays unallocated rather than landing on the unissued invoice.
        (await f.Db.Payments.SingleAsync()).UnallocatedAmount.Should().Be(1500m - 1160m);
        var stillDraft = await f.Db.Invoices.SingleAsync(i => i.Id != issuedInv.Id);
        stillDraft.Status.Should().Be(InvoiceStatus.Draft);
        stillDraft.PaidAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Overpayment_is_carried_as_unallocated_rather_than_creating_a_negative_balance()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");
        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        await f.Receipts.CreateAsync(ReceiptFor(created.Id, issued.Total + 500m), "bob");

        // Math.Min caps the allocation at the balance, and the remainder stays on the payment as
        // unallocated — money on account. Asserted because the alternative reading of "overpayment
        // is refused" from #212's spec is a deliberate divergence, not an oversight: a customer who
        // pays too much has still paid, and refusing the receipt would lose the record of it.
        var after = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        after.Balance.Should().Be(0m);
        after.PaidAmount.Should().Be(issued.Total);
        var payment = await f.Db.Payments.SingleAsync();
        payment.UnallocatedAmount.Should().Be(500m);
    }
}
