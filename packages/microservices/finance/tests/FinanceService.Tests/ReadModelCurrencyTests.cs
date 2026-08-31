using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// The transaction currency must survive the trip out to a caller.
///
/// <para>It did not. Currency was captured on the write side, validated against a configured exchange
/// rate, persisted as <c>CurrencyId</c> — and then <b>silently dropped on read</b>: four of the five
/// finance read DTOs had no currency field at all, and every <c>CurrencyCode</c> in <c>ApDtos</c>,
/// <c>JournalDtos</c> and <c>ImprestDtos</c> was on a <i>Create</i> DTO. So no consumer could tell a
/// USD record from a KES one, and the frontend labelling everything "Kshs" (#288) was the visible
/// symptom of a projection gap rather than a formatting bug.</para>
///
/// <para>These are deliberately about the read model, not the arithmetic — <see cref="ForeignCurrencyTests"/>
/// covers conversion. A DTO that carries the right number and the wrong currency is worse than one that
/// carries neither, because it looks correct.</para>
/// </summary>
public class ReadModelCurrencyTests
{
    [Fact]
    public async Task A_foreign_supplier_invoice_reports_its_currency()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Bill(1_000m);
        dto.CurrencyCode = "USD";
        var created = await f.Bills.CreateAsync(dto, "alice");

        var read = await f.Bills.GetAsync(created.Id);

        read!.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task A_base_currency_supplier_invoice_reports_the_base_code()
    {
        using var f = new LedgerFixture();
        var created = await f.Bills.CreateAsync(LedgerFixture.Bill(1_000m), "alice");

        var read = await f.Bills.GetAsync(created.Id);

        // Not null and not blank: a consumer that has to guess when the field is empty will guess wrong
        // for whichever currency it sees least often.
        read!.CurrencyCode.Should().Be("KES");
    }

    [Fact]
    public async Task The_supplier_invoice_LIST_carries_the_currency_too()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Bill(1_000m);
        dto.CurrencyCode = "USD";
        await f.Bills.CreateAsync(dto, "alice");

        var list = await f.Bills.ListAsync();

        // The list and the detail are separate projections, and a fix applied to one and not the other
        // is how a grid disagrees with the record it opens.
        list.Should().ContainSingle().Which.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task A_foreign_journal_reports_its_currency_in_detail_and_in_the_list()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "USD";
        var created = await f.Journals.CreateAsync(dto, "preparer");

        var detail = await f.Journals.GetAsync(created.Id);
        var list = await f.Journals.ListAsync();

        detail!.CurrencyCode.Should().Be("USD");
        list.Should().Contain(j => j.Id == created.Id && j.CurrencyCode == "USD");
    }

    [Fact]
    public async Task An_unknown_currency_id_reads_as_null_rather_than_a_wrong_code()
    {
        using var f = new LedgerFixture();
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(100m), "preparer");

        // Simulate a currency row removed after the fact — the id on the record no longer resolves.
        var entry = await f.Db.JournalEntries.SingleAsync(e => e.Id == created.Id);
        entry.CurrencyId = "ccy-deleted";
        await f.Db.SaveChangesAsync();

        var read = await f.Journals.GetAsync(created.Id);

        // Null means "unknown", which a caller can render as such. Falling back to the base currency
        // code would state a specific, wrong currency with full confidence — the same class of mistake
        // as the parity fallbacks removed in #226 and #245.
        read!.CurrencyCode.Should().BeNull();
    }
}
