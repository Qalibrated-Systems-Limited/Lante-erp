using FluentAssertions;
using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// What multi-currency actually does today.
///
/// <para>Written for the #226 audit. Currency is <i>recorded</i> far more widely than it is
/// <i>handled</i>, and partial multi-currency is worse than none because it looks finished — a USD
/// invoice stores a currency, the figures look right, and the gap only appears at period close.
/// These tests pin the real behaviour so the boundary is documented rather than assumed.</para>
///
/// <para>Assertions marked <c>BUG:</c> pin behaviour that is wrong and pass today. They fail when the
/// defect is fixed, which is the point — the fix has to come here and delete them deliberately.</para>
/// </summary>
public class ForeignCurrencyTests
{
    private static FinanceService.Core.DTOs.CreateJournalDto Usd(decimal amount, System.DateTime? date = null)
    {
        var dto = LedgerFixture.BalancedJournal(amount, date);
        dto.CurrencyCode = "USD";
        return dto;
    }

    private static async Task<List<JournalLine>> LinesOf(LedgerFixture f, string entryId) =>
        await f.Db.JournalLines.AsNoTracking().Where(l => l.JournalEntryId == entryId).ToListAsync();

    // ── What works ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_foreign_entry_is_converted_to_base_by_multiplying()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "USD";

        var created = await f.Journals.CreateAsync(dto, "preparer");

        var lines = await f.Db.JournalLines.AsNoTracking().Where(l => l.JournalEntryId == created.Id).ToListAsync();
        // ExchangeRate is base units per 1 foreign unit (KES 130 per USD 1), so base = foreign × rate.
        // Dividing would turn USD 100 into KES 0.77 — and would have gone unnoticed, since every posting
        // to date has been in the base currency.
        lines.Sum(l => l.BaseDebit).Should().Be(13_000m);
        lines.Sum(l => l.BaseCredit).Should().Be(13_000m);
    }

    [Fact]
    public async Task A_journals_read_dto_carries_the_currency_it_was_actually_posted_in()
    {
        // #295 (part of #288's remainder): JournalReadDto never carried CurrencyCode back out,
        // same gap InvoiceReadDto/SupplierInvoiceReadDto/VoucherReadDto had. TotalDebit/TotalCredit
        // are the entry's OWN transaction-currency totals (not the base-currency amounts that feed
        // the trial balance), so displaying them without a currency label was exactly as wrong.
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "USD";
        var created = await f.Journals.CreateAsync(dto, "preparer");

        created.CurrencyCode.Should().Be("USD");
        created.TotalDebit.Should().Be(100m);   // transaction-currency total, not the 13,000 base amount
        (await f.Journals.GetAsync(created.Id))!.CurrencyCode.Should().Be("USD");
        (await f.Journals.ListAsync()).Single(j => j.Id == created.Id).CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task A_journal_raised_with_no_explicit_currency_reports_the_base_currency_code()
    {
        using var f = new LedgerFixture();
        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(1_000m), "preparer");

        created.CurrencyCode.Should().Be("KES");
    }

    [Fact]
    public async Task The_rate_used_is_stamped_on_the_line_and_survives_a_later_rate_change()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "USD";
        var created = await f.Journals.CreateAsync(dto, "preparer");

        var usd = await f.Db.Currencies.SingleAsync(c => c.Code == "USD");
        usd.ExchangeRate = 200m;
        await f.Db.SaveChangesAsync();

        var lines = await f.Db.JournalLines.AsNoTracking().Where(l => l.JournalEntryId == created.Id).ToListAsync();
        // This is the genuinely sound part of the design: a posted line keeps the rate it was posted at,
        // so revaluing the currency cannot silently restate history.
        lines.Should().OnlyContain(l => l.FxRate == 130m);
        lines.Sum(l => l.BaseDebit).Should().Be(13_000m);
    }

    [Fact]
    public async Task A_base_currency_entry_is_stamped_at_parity()
    {
        using var f = new LedgerFixture();

        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(1_000m), "preparer");

        var lines = await f.Db.JournalLines.AsNoTracking().Where(l => l.JournalEntryId == created.Id).ToListAsync();
        lines.Should().OnlyContain(l => l.FxRate == 1m);
        lines.Sum(l => l.BaseDebit).Should().Be(1_000m);
    }

    [Fact]
    public async Task A_foreign_entry_balances_in_base_currency_too()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(333.33m);
        dto.CurrencyCode = "USD";

        var created = await f.Journals.CreateAsync(dto, "preparer");

        var lines = await f.Db.JournalLines.AsNoTracking().Where(l => l.JournalEntryId == created.Id).ToListAsync();
        // Balancing in transaction currency is not enough — the ledger is kept in base. If rounding were
        // applied per line without the totals agreeing, the trial balance would drift by cents per entry.
        lines.Sum(l => l.BaseDebit).Should().Be(lines.Sum(l => l.BaseCredit));
    }

    [Fact]
    public async Task A_foreign_invoice_posts_its_journal_in_the_invoice_currency()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Invoice(1_000m);
        dto.CurrencyCode = "USD";
        var created = await f.Invoices.CreateAsync(dto, "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var entry = await f.Db.JournalEntries.AsNoTracking().SingleAsync(e => e.SourceDocumentId == created.Id);
        var lines = await LinesOf(f, entry.Id);
        // The AR subledger and the general ledger have to agree about what the customer was billed. The
        // invoice carried a CurrencyId while the journal it posted carried none, so a USD 1,000 invoice
        // booked a KES 1,000 journal — the two records differing by the exchange rate, with nothing
        // reconciling them and nothing that would show up as an imbalance (#226).
        lines.Should().OnlyContain(l => l.FxRate == 130m);
        lines.Sum(l => l.BaseDebit).Should().Be(1_160m * 130m);   // gross 1,160 USD at 130
    }

    [Fact]
    public async Task An_invoices_read_dto_carries_the_currency_it_was_actually_raised_in()
    {
        // #288: InvoiceReadDto never carried CurrencyCode back out at all, so the frontend had no
        // way to render anything but the base currency's label — a USD 1,000 invoice displayed and
        // exported as "Kshs 1,000.00", regardless of what CreateInvoiceDto/the entity actually held.
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Invoice(1_000m);
        dto.CurrencyCode = "USD";
        var created = await f.Invoices.CreateAsync(dto, "alice");

        created.CurrencyCode.Should().Be("USD");
        (await f.Invoices.GetAsync(created.Id))!.CurrencyCode.Should().Be("USD");
        (await f.Invoices.ListAsync()).Single(i => i.Id == created.Id).CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task An_invoice_raised_with_no_explicit_currency_reports_the_base_currency_code()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1_000m), "alice");

        created.CurrencyCode.Should().Be("KES");
    }

    [Fact]
    public async Task A_foreign_supplier_invoice_posts_its_journal_in_the_bill_currency()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Bill(1_000m);
        dto.CurrencyCode = "USD";
        var created = await f.Bills.CreateAsync(dto, "alice");

        await f.Bills.ApproveAsync(created.Id, "bob");

        var entry = await f.Db.JournalEntries.AsNoTracking().SingleAsync(e => e.SourceDocumentId == created.Id);
        var lines = await LinesOf(f, entry.Id);
        lines.Should().OnlyContain(l => l.FxRate == 130m);
    }

    [Fact]
    public async Task An_invoice_in_an_unconfigured_currency_is_refused_at_creation()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Invoice(1_000m);
        dto.CurrencyCode = "USE";       // a typo for USD

        var act = () => f.Invoices.CreateAsync(dto, "alice");

        // Caught when the invoice is raised, not discovered at period close. Falling back to base meant the
        // customer was billed in shillings while everyone believed the figure was dollars.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*USE is not configured*");
    }

    [Fact]
    public async Task Paying_a_foreign_bill_posts_the_payment_in_the_bills_own_currency()
    {
        // Payment side of the same #226 class of defect invoice-issuing/bill-approval were already
        // fixed for (#264): PaymentVoucherService.CreateAsync used to stamp CurrencyId to the base
        // currency unconditionally, and PayAsync's journal never set CurrencyCode at all — even
        // though the voucher's Amount is drawn straight from the bill's own balance. A $1,160
        // payment posted as a KES 1,160 journal at parity, silently misstating the ledger by the
        // exchange rate. Found and fixed together (no prior test exercised PayAsync's posting at
        // all).
        using var f = new LedgerFixture();
        var billDto = LedgerFixture.Bill(1_000m);
        billDto.CurrencyCode = "USD";
        var bill = await f.Bills.CreateAsync(billDto, "alice");
        await f.Bills.ApproveAsync(bill.Id, "bob");

        var voucher = await f.Vouchers.CreateAsync(new CreateVoucherDto { SupplierInvoiceId = bill.Id }, "alice");
        await f.Vouchers.ApproveAsync(voucher.Id, "bob");

        // PayAsync posts at DateTime.UtcNow.Date (today, whenever the test runs), not the bill's own
        // date — the fixture's seeded period covers a fixed month in the past, so an open period
        // covering today is needed here, independent of that.
        f.Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = "per-today", FiscalYearId = "fy-today", PeriodNo = 1, Name = "today",
            StartDate = DateTime.UtcNow.Date.AddDays(-1), EndDate = DateTime.UtcNow.Date.AddDays(1),
            Status = PeriodStatus.Open,
        });
        await f.Db.SaveChangesAsync();

        var paid = await f.Vouchers.PayAsync(voucher.Id, "alice");

        paid.CurrencyCode.Should().Be("USD");

        var entry = await f.Db.JournalEntries.AsNoTracking().SingleAsync(e => e.SourceDocumentId == voucher.Id);
        var lines = await LinesOf(f, entry.Id);
        lines.Should().OnlyContain(l => l.FxRate == 130m);
        lines.Sum(l => l.BaseDebit).Should().Be(1_160m * 130m);   // gross 1,160 USD at 130
    }

    [Fact]
    public async Task Disbursing_and_retiring_a_foreign_imprest_posts_both_journals_in_its_own_currency()
    {
        // Same #226/#264 class of defect as the voucher case above, found the same way while
        // wiring up this DTO's CurrencyCode: ImprestService.DisburseAsync and RetireAsync both
        // built their CreateJournalDto with no CurrencyCode at all, even though the request's own
        // CurrencyId is resolved and stored correctly at CreateAsync. A USD imprest's disbursement
        // and retirement both posted at base-currency parity. No prior test exercised either
        // posting.
        using var f = new LedgerFixture();
        f.Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = "per-today", FiscalYearId = "fy-today", PeriodNo = 1, Name = "today",
            StartDate = DateTime.UtcNow.Date.AddDays(-1), EndDate = DateTime.UtcNow.Date.AddDays(1),
            Status = PeriodStatus.Open,
        });
        await f.Db.SaveChangesAsync();

        var req = await f.Imprests.CreateAsync(
            new CreateImprestDto { EmployeeName = "Asha", Purpose = "Site trip", Amount = 100m, CurrencyCode = "USD" }, "alice");
        req.CurrencyCode.Should().Be("USD");

        await f.Imprests.ApproveAsync(req.Id, "bob");
        var disbursed = await f.Imprests.DisburseAsync(req.Id, "cara");
        disbursed.CurrencyCode.Should().Be("USD");

        var disburseEntry = await f.Db.JournalEntries.AsNoTracking().SingleAsync(e => e.SourceDocumentId == req.Id);
        (await LinesOf(f, disburseEntry.Id)).Should().OnlyContain(l => l.FxRate == 130m);

        var retired = await f.Imprests.RetireAsync(req.Id,
            new RetireImprestDto { Lines = { new RetireLineDto { Description = "Fuel", Amount = 100m } } }, "alice");
        retired.CurrencyCode.Should().Be("USD");

        var retireEntry = await f.Db.JournalEntries.AsNoTracking()
            .SingleAsync(e => e.SourceDocumentId == req.Id && e.Id != disburseEntry.Id);
        (await LinesOf(f, retireEntry.Id)).Should().OnlyContain(l => l.FxRate == 130m);
    }

    [Fact]
    public async Task An_overdue_foreign_imprest_converts_to_a_base_currency_advance()
    {
        // #302: PersonalAdvance has no currency of its own — it's recovered through payroll/final
        // settlement in base currency (HR's DisciplineService sums it straight into a KES
        // separation-clearance figure). RunConversionsAsync used to copy UnretiredBalance across
        // with no FX conversion, so a $100 unretired USD imprest became a "100" advance — meant to
        // be recovered as KES 13,000, actually recoverable as KES 100.
        using var f = new LedgerFixture();
        f.Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = "per-today", FiscalYearId = "fy-today", PeriodNo = 1, Name = "today",
            StartDate = DateTime.UtcNow.Date.AddDays(-1), EndDate = DateTime.UtcNow.Date.AddDays(1),
            Status = PeriodStatus.Open,
        });
        await f.Db.SaveChangesAsync();

        var req = await f.Imprests.CreateAsync(
            new CreateImprestDto { EmployeeName = "Asha", Purpose = "Site trip", Amount = 100m, CurrencyCode = "USD" }, "alice");
        await f.Imprests.ApproveAsync(req.Id, "bob");
        await f.Imprests.DisburseAsync(req.Id, "cara");

        var pastDue = DateTime.UtcNow.Date.AddDays(15);   // DueDate is disbursement + 14 days
        var advances = await f.Imprests.RunConversionsAsync(pastDue, "system");

        advances.Should().HaveCount(1);
        advances[0].Amount.Should().Be(13_000m);   // 100 USD at 130, not the raw 100
    }

    [Fact]
    public async Task An_overdue_base_currency_imprest_converts_to_an_advance_of_the_same_amount()
    {
        using var f = new LedgerFixture();
        f.Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = "per-today", FiscalYearId = "fy-today", PeriodNo = 1, Name = "today",
            StartDate = DateTime.UtcNow.Date.AddDays(-1), EndDate = DateTime.UtcNow.Date.AddDays(1),
            Status = PeriodStatus.Open,
        });
        await f.Db.SaveChangesAsync();

        var req = await f.Imprests.CreateAsync(
            new CreateImprestDto { EmployeeName = "Asha", Purpose = "Site trip", Amount = 5_000m }, "alice");
        await f.Imprests.ApproveAsync(req.Id, "bob");
        await f.Imprests.DisburseAsync(req.Id, "cara");

        var advances = await f.Imprests.RunConversionsAsync(DateTime.UtcNow.Date.AddDays(15), "system");

        advances.Should().ContainSingle().Which.Amount.Should().Be(5_000m);
    }

    // ── What does not work ───────────────────────────────────────────────────────

    [Fact]
    public async Task Two_entries_for_the_same_trading_day_get_different_rates_if_keyed_at_different_times()
    {
        using var f = new LedgerFixture();
        var tradingDay = LedgerFixture.InPeriod;

        var first = await f.Journals.CreateAsync(Usd(100m, tradingDay), "preparer");

        // The rate moves — a new day's rate is written over the old one, because there is only one field.
        var usd = await f.Db.Currencies.SingleAsync(c => c.Code == "USD");
        usd.ExchangeRate = 200m;
        await f.Db.SaveChangesAsync();

        // The same trading day, entered late — a paper invoice that reached accounts a week after the fact.
        var second = await f.Journals.CreateAsync(Usd(100m, tradingDay), "preparer");

        var firstLines  = await LinesOf(f, first.Id);
        var secondLines = await LinesOf(f, second.Id);

        // BUG: EntryDate plays no part in choosing the rate. JournalService reads `ccy.ExchangeRate`, a
        // single mutable field on the currency row, so the rate applied is the one current at the moment of
        // KEYING, not the one that applied on the transaction date. There is no dated rate table, so the
        // rate for a given date is not merely unused — it is unavailable.
        //
        // The consequence is that the base-currency value of a day's trading is not reproducible: two
        // identical USD 100 transactions on the same day are booked at KES 13,000 and KES 20,000 purely
        // because of when someone typed them in. Nothing reconciles the 7,000 difference, because realised
        // and unrealised FX gain/loss do not exist (#226).
        firstLines.Should().OnlyContain(l => l.FxRate == 130m);
        secondLines.Should().OnlyContain(l => l.FxRate == 200m);
        secondLines.Sum(l => l.BaseDebit).Should().Be(20_000m);
    }

    [Fact]
    public async Task A_currency_with_no_rate_set_cannot_be_posted_in()
    {
        using var f = new LedgerFixture();
        f.Db.Currencies.Add(new Currency
        {
            Id = "ccy-eur", Code = "EUR", Name = "Euro", IsBaseCurrency = false, ExchangeRate = 0m,
        });
        await f.Db.SaveChangesAsync();

        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "EUR";
        var act = () => f.Journals.CreateAsync(dto, "preparer");

        // Previously `ccy.ExchangeRate == 0 ? 1m : ...` booked EUR 100 as KES 100 — a hundredfold
        // understatement that balanced perfectly and raised nothing. A rate of zero means nobody has said
        // what the currency is worth, which is not the same as it being worth one shilling.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No exchange rate is set for EUR*");
        (await f.Db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_base_currency_still_posts_even_if_its_rate_row_was_never_filled_in()
    {
        using var f = new LedgerFixture();
        var kes = await f.Db.Currencies.SingleAsync(c => c.IsBaseCurrency);
        kes.ExchangeRate = 0m;
        await f.Db.SaveChangesAsync();

        var created = await f.Journals.CreateAsync(LedgerFixture.BalancedJournal(1_000m), "preparer");

        // The base currency is 1 by definition. Refusing here would take the whole ledger down over a blank
        // field that cannot mean anything else — the refusal above is only meaningful for a currency whose
        // value genuinely is unknown.
        var lines = await LinesOf(f, created.Id);
        lines.Should().OnlyContain(l => l.FxRate == 1m);
        lines.Sum(l => l.BaseDebit).Should().Be(1_000m);
    }

    [Fact]
    public async Task Nothing_ever_writes_a_forex_revaluation()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "USD";
        await f.Journals.CreateAsync(dto, "preparer");

        var usd = await f.Db.Currencies.SingleAsync(c => c.Code == "USD");
        usd.ExchangeRate = 200m;          // a 54% move against an open foreign balance
        await f.Db.SaveChangesAsync();

        // The ForexRevaluationLog entity, its DbSet and its table all exist and are shipped to every tenant
        // schema. No service code writes or reads any of them — verified by grep across the service. So the
        // table is permanently empty and revaluation is not "incomplete", it is absent, while looking
        // present to anyone reading the schema. Same shape as the orphaned backup CronJob in #198:
        // committed, correct-looking, and never running.
        (await f.Db.ForexRevaluationLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_currency_is_refused_rather_than_treated_as_the_base_currency()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.BalancedJournal(100m);
        dto.CurrencyCode = "USE";        // a typo for USD

        var act = () => f.Journals.CreateAsync(dto, "preparer");

        // Falling back to base booked a foreign amount as shillings with nothing to review. A code nobody
        // configured is a mistake, and the cheapest place to catch it is before it reaches the ledger.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*USE is not configured*");
        (await f.Db.JournalEntries.CountAsync()).Should().Be(0);
    }
}
