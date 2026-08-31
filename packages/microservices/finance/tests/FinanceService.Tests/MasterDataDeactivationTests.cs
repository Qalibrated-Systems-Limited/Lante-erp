using FluentAssertions;
using FinanceService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// #332 — six master-data entities (Customer, Supplier, ChartOfAccount, CostCenter, Currency,
/// TaxCategory) carry <c>IsActive</c>, written in exactly one place (the seeder) and read in
/// exactly one place before this change (a response DTO projection that guarded nothing). A
/// deactivated supplier could still be billed, a deactivated account could still be posted to.
///
/// Each test here deactivates the entity directly against the fixture's DbContext (bypassing the
/// deactivate endpoint, which is a thin DbContext write with nothing to unit test) and then drives
/// the real write path that should now refuse it.
/// </summary>
public class MasterDataDeactivationTests
{
    [Fact]
    public async Task An_invoice_cannot_be_raised_against_a_deactivated_customer()
    {
        using var fx = new LedgerFixture();
        var customer = await fx.Db.Customers.FindAsync(LedgerFixture.CustomerId);
        customer!.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var act = () => fx.Invoices.CreateAsync(LedgerFixture.Invoice(), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task A_bill_cannot_be_raised_against_a_deactivated_supplier()
    {
        using var fx = new LedgerFixture();
        var supplier = await fx.Db.Suppliers.FindAsync(LedgerFixture.SupplierId);
        supplier!.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var act = () => fx.Bills.CreateAsync(LedgerFixture.Bill(), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task A_journal_cannot_post_to_a_deactivated_account()
    {
        using var fx = new LedgerFixture();
        var account = await fx.Db.ChartOfAccounts.FindAsync(LedgerFixture.CashAccountId);
        account!.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var act = () => fx.Journals.CreateAsync(LedgerFixture.BalancedJournal(), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task A_journal_line_cannot_be_assigned_a_deactivated_cost_centre()
    {
        using var fx = new LedgerFixture();
        fx.Db.CostCenters.Add(new CostCenter { Id = "cc-1", Code = "CC1", Name = "Ops", IsActive = false });
        await fx.Db.SaveChangesAsync();

        var dto = LedgerFixture.BalancedJournal();
        dto.Lines[0].CostCenterId = "cc-1";

        var act = () => fx.Journals.CreateAsync(dto, "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task A_journal_line_referencing_a_nonexistent_cost_centre_is_refused()
    {
        // Previously silent: CostCenterId flowed straight onto the line with no lookup at all.
        using var fx = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal();
        dto.Lines[0].CostCenterId = "does-not-exist";

        var act = () => fx.Journals.CreateAsync(dto, "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task A_journal_cannot_post_in_a_deactivated_currency()
    {
        using var fx = new LedgerFixture();
        var usd = await fx.Db.Currencies.FirstAsync(c => c.Code == "USD");
        usd.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var dto = LedgerFixture.BalancedJournal();
        dto.CurrencyCode = "USD";

        var act = () => fx.Journals.CreateAsync(dto, "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task A_new_invoice_line_cannot_use_a_deactivated_tax_category()
    {
        using var fx = new LedgerFixture();
        var standard = await fx.Db.TaxCategories.FirstAsync(t => t.Code == "A");
        standard.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var act = () => fx.Invoices.CreateAsync(LedgerFixture.Invoice(taxCode: "A"), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task A_new_bill_line_cannot_use_a_deactivated_tax_category()
    {
        using var fx = new LedgerFixture();
        var standard = await fx.Db.TaxCategories.FirstAsync(t => t.Code == "A");
        standard.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var act = () => fx.Bills.CreateAsync(LedgerFixture.Bill(taxCode: "A"), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deactivated*");
    }

    [Fact]
    public async Task None_of_the_guards_fire_when_everything_is_active()
    {
        // The fixture's default state — every entity IsActive = true. Pins that these six new
        // checks don't accidentally refuse the common case.
        using var fx = new LedgerFixture();
        fx.Db.CostCenters.Add(new CostCenter { Id = "cc-1", Code = "CC1", Name = "Ops", IsActive = true });
        await fx.Db.SaveChangesAsync();

        var journalDto = LedgerFixture.BalancedJournal();
        journalDto.Lines[0].CostCenterId = "cc-1";

        await fx.Journals.CreateAsync(journalDto, "tester");
        await fx.Invoices.CreateAsync(LedgerFixture.Invoice(), "tester");
        await fx.Bills.CreateAsync(LedgerFixture.Bill(), "tester");
    }
}
