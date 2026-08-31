using FinanceService.Core.Enums;
using FluentAssertions;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Enum ordinals that are a stored wire format, pinned so they cannot move.
///
/// <para>EF persists these as <c>integer</c> columns, so the numbers below are in every row of
/// Invoices and SupplierInvoices in every tenant schema. Renumbering them does not fail a build, does
/// not fail any other test, and does not throw at runtime — it silently changes what existing rows
/// mean. That is the worst failure shape available: no error, wrong answer.</para>
///
/// <para>Written while removing <see cref="InvoiceStatus"/>'s unreachable Overdue member (#343). The
/// obvious edit — deleting it from the middle of the list — would have shifted Cancelled from 5 to 4,
/// so every invoice already cancelled would have come back as an undefined value, dropped out of the
/// <c>Status != Cancelled</c> filters in AgingAsync, VatService and CashFlowService, and reappeared as
/// a live receivable owed by a customer who owes nothing. #346 made Cancelled reachable, so those rows
/// exist. Nothing in the suite would have caught it.</para>
///
/// <para>These tests deliberately restate constants. That is the point: an assertion that duplicates a
/// value is worthless when the value is free to change, and load-bearing when it is not.</para>
/// </summary>
public class PersistedEnumOrdinalTests
{
    [Theory]
    [InlineData(InvoiceStatus.Draft, 0)]
    [InlineData(InvoiceStatus.Issued, 1)]
    [InlineData(InvoiceStatus.PartPaid, 2)]
    [InlineData(InvoiceStatus.Paid, 3)]
    // 4 was Overdue, removed in #343 and deliberately left unused.
    [InlineData(InvoiceStatus.Cancelled, 5)]
    public void InvoiceStatus_ordinals_are_fixed(InvoiceStatus status, int stored)
    {
        ((int)status).Should().Be(stored);
    }

    [Fact]
    public void InvoiceStatus_has_no_member_at_4()
    {
        // The gap Overdue left. Reusing it would give a new meaning to a value that may sit in old
        // rows, and would make this file's other assertions pass while the data changed underneath.
        Enum.IsDefined(typeof(InvoiceStatus), 4).Should().BeFalse();
    }

    [Fact]
    public void InvoiceStatus_no_longer_defines_Overdue()
    {
        // Nothing ever assigned it (zero `= InvoiceStatus.Overdue` sites) and aging is computed from
        // DueDate at query time, so the member only invited a sweep that would have fought
        // ReceiptService's status writes. See #343.
        Enum.GetNames<InvoiceStatus>().Should().NotContain("Overdue");
    }

    [Theory]
    [InlineData(SupplierInvoiceStatus.Received, 0)]
    [InlineData(SupplierInvoiceStatus.Approved, 1)]
    [InlineData(SupplierInvoiceStatus.PartPaid, 2)]
    [InlineData(SupplierInvoiceStatus.Paid, 3)]
    [InlineData(SupplierInvoiceStatus.Cancelled, 4)]
    public void SupplierInvoiceStatus_ordinals_are_fixed(SupplierInvoiceStatus status, int stored)
    {
        // Pinned even though this PR does not change it: it is persisted the same way, sits in the
        // same class of hazard, and AP is where the next status will get inserted "in the right
        // place" alphabetically or logically rather than appended.
        ((int)status).Should().Be(stored);
    }

    [Theory]
    [InlineData(JournalStatus.Draft, 0)]
    [InlineData(JournalStatus.PendingReview, 1)]
    [InlineData(JournalStatus.PendingApproval, 2)]
    [InlineData(JournalStatus.Posted, 3)]
    [InlineData(JournalStatus.Reversed, 4)]
    public void JournalStatus_ordinals_are_fixed(JournalStatus status, int stored)
    {
        // The general ledger's own lifecycle. Reversed moving would reclassify posted entries, which
        // is the same defect one layer further down.
        ((int)status).Should().Be(stored);
    }

    [Theory]
    [InlineData(PeriodStatus.Open, 0)]
    [InlineData(PeriodStatus.Closed, 1)]
    [InlineData(PeriodStatus.Locked, 2)]
    public void PeriodStatus_ordinals_are_fixed(PeriodStatus status, int stored)
    {
        // #342 made the close a real lock. If Open stopped being 0, closed periods would read as open
        // and the lock would quietly stop holding.
        ((int)status).Should().Be(stored);
    }
}
