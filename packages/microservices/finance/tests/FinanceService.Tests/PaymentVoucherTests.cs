using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Payment voucher mechanics against a supplier bill (#230) — the AP mirror of
/// <see cref="ReceiptAllocationTests"/>. <c>PaymentVoucherService.PayAsync</c> posts today, so every
/// test that reaches it seeds an <see cref="AccountingPeriod"/> covering
/// <c>DateTime.UtcNow.Date</c>, same as <see cref="ForeignCurrencyTests"/>.
///
/// <para><b>Two findings pinned here, not fixed (test-only scope):</b> unlike
/// <see cref="ReceiptService"/>, which explicitly carries an AR overpayment as unallocated rather
/// than driving a balance negative, <c>PaymentVoucherService.CreateAsync</c> validates only that a
/// bill isn't still <c>Received</c> (unapproved) — it does not cap <c>dto.Amount</c> against
/// <c>bill.Balance</c>, and it does not block raising a voucher against a bill that is already
/// <c>Paid</c> or <c>Cancelled</c>. Both are characterized below as today's real behaviour, not
/// endorsed — same shape as #229's characterization of the three-way-match guard.</para>
/// </summary>
public class PaymentVoucherTests
{
    private static void SeedTodayPeriod(LedgerFixture f)
    {
        f.Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = "per-today", FiscalYearId = "fy-today", PeriodNo = 1, Name = "today",
            StartDate = DateTime.UtcNow.Date.AddDays(-1), EndDate = DateTime.UtcNow.Date.AddDays(1),
            Status = PeriodStatus.Open,
        });
    }

    private async Task<string> ApprovedBillAsync(LedgerFixture f, decimal unitPrice)
    {
        var bill = await f.Bills.CreateAsync(LedgerFixture.Bill(unitPrice), "alice");
        await f.Bills.ApproveAsync(bill.Id, "bob");
        return bill.Id;
    }

    private static Task<SupplierInvoice> RowAsync(LedgerFixture f, string billId) =>
        f.Db.SupplierInvoices.SingleAsync(b => b.Id == billId);

    // ── The happy paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_partial_voucher_leaves_the_bill_part_paid()
    {
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);   // total 1,160 with #A tax at 16%
        var before = await RowAsync(f, billId);

        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId, Amount = 500m }, "alice");
        await f.Vouchers.ApproveAsync(voucher.Id, "bob");
        await f.Vouchers.PayAsync(voucher.Id, "alice");

        var after = await RowAsync(f, billId);
        after.Status.Should().Be(SupplierInvoiceStatus.PartPaid);
        after.PaidAmount.Should().Be(500m);
        after.Balance.Should().Be(before.Total - 500m);
    }

    [Fact]
    public async Task A_voucher_for_the_full_balance_settles_the_bill()
    {
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);

        // Amount omitted — CreateAsync defaults it to the bill's current balance.
        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId }, "alice");
        await f.Vouchers.ApproveAsync(voucher.Id, "bob");
        var paid = await f.Vouchers.PayAsync(voucher.Id, "alice");

        paid.Status.Should().Be(nameof(VoucherStatus.Paid));
        var bill = await RowAsync(f, billId);
        bill.Status.Should().Be(SupplierInvoiceStatus.Paid);
        bill.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task Successive_vouchers_accumulate_rather_than_overwrite()
    {
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);   // total 1,160

        var v1 = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId, Amount = 400m }, "alice");
        await f.Vouchers.ApproveAsync(v1.Id, "bob");
        await f.Vouchers.PayAsync(v1.Id, "alice");

        var v2 = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId, Amount = 300m }, "alice");
        await f.Vouchers.ApproveAsync(v2.Id, "bob");
        await f.Vouchers.PayAsync(v2.Id, "alice");

        var afterTwo = await RowAsync(f, billId);
        afterTwo.PaidAmount.Should().Be(700m);
        afterTwo.Status.Should().Be(SupplierInvoiceStatus.PartPaid);

        // Final voucher for exactly what's left.
        var v3 = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId }, "alice");
        await f.Vouchers.ApproveAsync(v3.Id, "bob");
        await f.Vouchers.PayAsync(v3.Id, "alice");

        var afterThree = await RowAsync(f, billId);
        afterThree.PaidAmount.Should().Be(afterThree.Total);
        afterThree.Balance.Should().Be(0m);
        afterThree.Status.Should().Be(SupplierInvoiceStatus.Paid);
    }

    [Fact]
    public async Task Each_journal_from_a_partial_voucher_balances_on_its_own()
    {
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);

        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId, Amount = 250m }, "alice");
        await f.Vouchers.ApproveAsync(voucher.Id, "bob");
        await f.Vouchers.PayAsync(voucher.Id, "alice");

        var entry = await f.Db.JournalEntries.SingleAsync(e => e.SourceDocumentId == voucher.Id);
        var lines = await f.Db.GeneralLedgerEntries.Where(l => l.JournalEntryId == entry.Id).ToListAsync();
        lines.Sum(l => l.Debit).Should().Be(lines.Sum(l => l.Credit));
        lines.Sum(l => l.Debit).Should().Be(250m);
    }

    // ── Guards that already exist ───────────────────────────────────────────────

    [Fact]
    public async Task A_voucher_against_an_unapproved_bill_is_refused()
    {
        using var f = new LedgerFixture();
        var bill = await f.Bills.CreateAsync(LedgerFixture.Bill(1_000m), "alice");   // still Received, not approved

        var act = () => f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = bill.Id }, "alice");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Approve the supplier invoice*");
    }

    [Fact]
    public async Task An_ad_hoc_voucher_needs_a_payee_and_a_positive_amount()
    {
        using var f = new LedgerFixture();

        var noPayee = () => f.Vouchers.CreateAsync(new CreateVoucherDto { Amount = 100m }, "alice");
        await noPayee.Should().ThrowAsync<InvalidOperationException>();

        var zeroAmount = () => f.Vouchers.CreateAsync(new CreateVoucherDto { Payee = "Landlord", Amount = 0m }, "alice");
        await zeroAmount.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_voucher_must_be_approved_before_it_can_be_paid()
    {
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);
        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId }, "alice");

        var act = () => f.Vouchers.PayAsync(voucher.Id, "alice");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*approve it before paying*");
    }

    // ── Characterization: gaps found while writing this suite, not fixed here ──

    [Fact]
    public async Task CHARACTERIZATION_a_voucher_larger_than_the_bills_balance_is_not_refused_today()
    {
        // Unlike ReceiptAllocationTests' Overpayment_is_carried_as_unallocated_rather_than_creating_a_negative_balance,
        // there is no equivalent guard here: CreateAsync only checks amount > 0, never amount <= bill.Balance.
        // Pinning today's real behaviour rather than guessing whether AP should refuse, cap, or carry an
        // overpayment the way AR does — that is a product decision, not a test-writing one. Worth its own
        // issue: this currently drives a supplier bill's Balance negative with no signal anywhere.
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);   // total 1,160
        var bill = await RowAsync(f, billId);

        var voucher = await f.Vouchers.CreateAsync(
            new CreateVoucherDto { SupplierInvoiceId = billId, Amount = bill.Total + 500m }, "alice");
        await f.Vouchers.ApproveAsync(voucher.Id, "bob");
        await f.Vouchers.PayAsync(voucher.Id, "alice");

        var after = await RowAsync(f, billId);
        after.Balance.Should().Be(-500m, "today's code has no cap — this is the gap, not the intended behaviour");
        after.Status.Should().Be(SupplierInvoiceStatus.Paid);
    }

    [Fact]
    public async Task CHARACTERIZATION_a_second_voucher_against_an_already_paid_bill_is_not_refused_today()
    {
        // CreateAsync's only status guard blocks Received (unapproved); it does not block Paid or
        // Cancelled. Pinning it, not endorsing it — same characterization-not-fix reasoning as #229.
        using var f = new LedgerFixture();
        SeedTodayPeriod(f);
        await f.Db.SaveChangesAsync();
        var billId = await ApprovedBillAsync(f, 1_000m);
        var first = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId }, "alice");
        await f.Vouchers.ApproveAsync(first.Id, "bob");
        await f.Vouchers.PayAsync(first.Id, "alice");
        (await RowAsync(f, billId)).Status.Should().Be(SupplierInvoiceStatus.Paid);

        // A second voucher against a bill that is now fully Paid is still accepted at creation time.
        var second = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = billId, Amount = 100m }, "alice");

        second.Should().NotBeNull();
        second.SupplierInvoiceId.Should().Be(billId);
    }
}
