using FinanceService.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Accounts-receivable posting.
///
/// <para>
/// Issuing an invoice is where the AR subledger meets the general ledger, so a defect here
/// shows up twice: on the customer statement and in the trial balance, disagreeing with each
/// other. These tests assert the posting is balanced, lands on the right accounts, and cannot
/// be repeated.
/// </para>
/// </summary>
public class InvoicePostingTests
{
    private const string ReceivableCode = "1200";
    private const string GovtReceivableCode = "1201";
    private const string VatOutputCode = "2200";
    private const string SalesCode = "4100";

    // ── The posting balances ─────────────────────────────────────────────────────

    [Fact]
    public async Task Issuing_an_invoice_posts_a_balanced_journal()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var entry = await SourceJournalAsync(f, created.Id);
        entry.TotalDebit.Should().Be(entry.TotalCredit);
    }

    [Fact]
    public async Task The_receivable_debit_equals_the_invoice_total()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m), "alice");

        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        var lines = await SourceJournalLinesAsync(f, created.Id);
        var receivable = lines.Single(l => AccountCodeOf(f, l.AccountId) == ReceivableCode);

        // The customer owes the gross amount, VAT included — not the net.
        receivable.Debit.Should().Be(issued.Total);
        receivable.Credit.Should().Be(0m);
    }

    [Fact]
    public async Task Revenue_is_credited_net_and_VAT_is_credited_separately()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m, taxCode: "A"), "alice");

        var issued = await f.Invoices.IssueAsync(created.Id, "alice");

        var lines = await SourceJournalLinesAsync(f, created.Id);
        var revenue = lines.Single(l => AccountCodeOf(f, l.AccountId) == SalesCode);
        var vat = lines.Single(l => AccountCodeOf(f, l.AccountId) == VatOutputCode);

        // Revenue must never include VAT — VAT is money collected on behalf of KRA, not income.
        revenue.Credit.Should().Be(1000m);
        vat.Credit.Should().Be(160m);
        (revenue.Credit + vat.Credit).Should().Be(issued.Total);
    }

    [Fact]
    public async Task An_exempt_line_posts_no_VAT_line_at_all()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(1000m, taxCode: "E"), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var lines = await SourceJournalLinesAsync(f, created.Id);
        // A zero-value VAT line would be noise on every exempt invoice, and zero-value lines are
        // exactly what the "debit XOR credit" invariant rejects.
        lines.Should().NotContain(l => AccountCodeOf(f, l.AccountId) == VatOutputCode);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_government_customer_posts_to_its_own_receivable_account()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(
            LedgerFixture.Invoice(1000m, customerId: LedgerFixture.GovtCustomerId), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var lines = await SourceJournalLinesAsync(f, created.Id);
        // Government receivables are tracked separately because they age differently and are
        // reported separately.
        lines.Should().Contain(l => AccountCodeOf(f, l.AccountId) == GovtReceivableCode);
        lines.Should().NotContain(l => AccountCodeOf(f, l.AccountId) == ReceivableCode);
    }

    // ── State guards ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_invoice_cannot_be_issued_twice()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(), "alice");
        await f.Invoices.IssueAsync(created.Id, "alice");

        var act = () => f.Invoices.IssueAsync(created.Id, "alice");

        // Double-issuing would post the revenue twice — the most expensive kind of duplicate.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only Draft invoices*");
    }

    [Fact]
    public async Task Issuing_an_unknown_invoice_is_a_not_found_rather_than_a_silent_no_op()
    {
        using var f = new LedgerFixture();

        var act = () => f.Invoices.IssueAsync("does-not-exist", "alice");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task An_invoice_needs_at_least_one_line()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Invoice();
        dto.Lines.Clear();

        var act = () => f.Invoices.CreateAsync(dto, "alice");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one line*");
    }

    [Fact]
    public async Task An_invoice_for_an_unknown_customer_is_refused()
    {
        using var f = new LedgerFixture();
        var dto = LedgerFixture.Invoice(customerId: "nobody");

        var act = () => f.Invoices.CreateAsync(dto, "alice");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Customer not found*");
    }

    // ── Linkage ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_invoice_records_which_journal_it_posted()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var invoice = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        // Without this link, reconciling the subledger to the GL means matching on amount and
        // date and hoping.
        invoice.JournalEntryId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_journal_records_which_invoice_produced_it()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var entry = await SourceJournalAsync(f, created.Id);
        entry.SourceModule.Should().Be("Finance-AR");
        entry.SourceDocumentId.Should().Be(created.Id);
    }

    [Fact]
    public async Task Issuing_posts_immediately_rather_than_leaving_a_draft_journal()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var entry = await SourceJournalAsync(f, created.Id);
        // An issued invoice the customer can see, backed by an unposted journal, is a
        // reconciliation gap.
        entry.Status.Should().Be(JournalStatus.Posted);
    }

    // ── Period interaction ───────────────────────────────────────────────────────

    [Fact]
    public async Task Issuing_into_a_closed_period_is_refused_by_the_ledger_underneath()
    {
        using var f = new LedgerFixture(periodStatus: PeriodStatus.Closed);
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(), "alice");

        var act = () => f.Invoices.IssueAsync(created.Id, "alice");

        // The period lock lives in JournalService, so this asserts AR actually goes through it
        // rather than writing to the ledger by another route.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Closed*");
    }

    // ── eTIMS ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Issuing_records_an_eTIMS_submission()
    {
        using var f = new LedgerFixture();
        var created = await f.Invoices.CreateAsync(LedgerFixture.Invoice(), "alice");

        await f.Invoices.IssueAsync(created.Id, "alice");

        var invoice = await f.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        invoice.EtimsReference.Should().NotBeNullOrEmpty();

        var submissions = await f.Db.EtimsSubmissions.Where(s => s.InvoiceId == created.Id).ToListAsync();
        submissions.Should().ContainSingle();
    }

    // NOTE: the provider behind this is StubEtimsProvider, which always accepts. These tests
    // therefore assert the *wiring* — that a submission is attempted and recorded — not that
    // KRA accepted anything. Real eTIMS is unimplemented and is tracked in #224. When the real
    // provider lands, the rejection path needs its own tests: what an invoice's status becomes
    // when KRA rejects it, and whether the GL posting stands or reverses.

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static async Task<Core.Entities.JournalEntry> SourceJournalAsync(LedgerFixture f, string invoiceId) =>
        await f.Db.JournalEntries.SingleAsync(e => e.SourceDocumentId == invoiceId);

    private static async Task<List<Core.Entities.JournalLine>> SourceJournalLinesAsync(LedgerFixture f, string invoiceId)
    {
        var entry = await SourceJournalAsync(f, invoiceId);
        return await f.Db.JournalLines.Where(l => l.JournalEntryId == entry.Id).ToListAsync();
    }

    private static string AccountCodeOf(LedgerFixture f, string accountId) =>
        f.Db.ChartOfAccounts.Single(a => a.Id == accountId).Code;
}
