using FinanceService.Core.DTOs;
using FinanceService.Core.Entities;
using FinanceService.Core.Enums;
using FinanceService.Infrastructure.Data;
using FinanceService.Infrastructure.External;
using FinanceService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceService.Tests;

/// <summary>
/// Builds an isolated in-memory ledger for a single test: one base currency, one open
/// accounting period, and a small chart of accounts including a header account that must not
/// accept postings.
///
/// <para>
/// <b>Why the in-memory provider is acceptable here, and where it is not.</b> Every rule these
/// tests exercise — balance, minimum lines, period status, header-account rejection, segregation
/// of duties, reversal direction — lives in C# inside <see cref="JournalService"/>, not in a
/// database constraint. The in-memory provider runs that logic faithfully.
/// </para>
/// <para>
/// It would be the wrong tool for anything that depends on the database actually behaving like
/// Postgres: unique indexes, foreign keys, transactions, and above all the schema-per-tenant
/// interceptor, which sets <c>search_path</c> on a real connection. Those need Testcontainers
/// against real Postgres. Do not extend this fixture to cover them — add a second one.
/// </para>
/// </summary>
public sealed class LedgerFixture : IDisposable
{
    public FinanceDbContext Db { get; }
    public JournalService Journals { get; }
    public InvoiceService Invoices { get; }
    public SupplierInvoiceService Bills { get; }
    public PaymentVoucherService Vouchers { get; }
    public ImprestService Imprests { get; }

    /// <summary>Receipts, so a part-payment test can drive the real allocation path rather than
    /// hand-setting the fields it would have written.</summary>
    public ReceiptService Receipts { get; }

    public const string CashAccountId = "acc-cash";
    public const string RevenueAccountId = "acc-revenue";
    public const string ExpenseAccountId = "acc-expense";
    public const string HeaderAccountId = "acc-header";
    public const string CustomerId = "cust-1";
    public const string GovtCustomerId = "cust-govt";
    public const string SupplierId = "supp-1";

    public static readonly DateTime InPeriod = new(2026, 6, 15);
    public static readonly DateTime OutsideAnyPeriod = new(2030, 1, 1);

    /// <summary>The period covering the real current date — what a reversal posts into.</summary>
    public const string CurrentPeriodId = "per-current";

    /// <summary>An open period in the previous calendar year, for cross-year reversal tests.</summary>
    public const string PriorYearPeriodId = "per-2025-12";

    /// <summary>A date inside <see cref="PriorYearPeriodId"/>.</summary>
    public static readonly DateTime InPriorYear = new(2025, 12, 15);

    public LedgerFixture(PeriodStatus periodStatus = PeriodStatus.Open, bool withBaseCurrency = true,
                         PeriodStatus? currentPeriodStatus = null)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            // A distinct database per fixture, so tests never see each other's rows.
            .UseInMemoryDatabase($"finance-tests-{Guid.NewGuid()}")
            .Options;

        Db = new FinanceDbContext(options);

        if (withBaseCurrency)
        {
            Db.Currencies.Add(new Currency
            {
                Id = "ccy-kes", Code = "KES", Name = "Kenyan Shilling",
                IsBaseCurrency = true, ExchangeRate = 1m,
            });
            Db.Currencies.Add(new Currency
            {
                Id = "ccy-usd", Code = "USD", Name = "US Dollar",
                IsBaseCurrency = false, ExchangeRate = 130m,
            });
        }

        Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = "per-2026-06", FiscalYearId = "fy-2026", PeriodNo = 6, Name = "2026-06",
            StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2026, 6, 30),
            Status = periodStatus,
        });

        // A prior-YEAR period, so a test can post in one year and reverse in another. Without it the
        // fixture's only two periods are both 2026, and a reversal taking its entry number from the
        // original's year instead of its own is indistinguishable from correct behaviour.
        Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = PriorYearPeriodId, FiscalYearId = "fy-2025", PeriodNo = 12, Name = "2025-12",
            StartDate = new DateTime(2025, 12, 1), EndDate = new DateTime(2025, 12, 31),
            Status = PeriodStatus.Open,
        });

        // A period covering TODAY, separate from the fixed June one every other test posts into.
        //
        // JournalService.ReverseAsync dates the reversal DateTime.UtcNow.Date, so it needs a period
        // covering the real current date — there is no clock to inject. Before the period fix it
        // needed no such thing, because it inherited the original entry's PeriodId and never looked
        // at its own date: the fixture happily produced a GL row dated today sitting inside period
        // 2026-06, and nothing noticed. Seeding this makes that incoherence impossible to write.
        //
        // Deliberately a DIFFERENT period from the June one, so a test can close June and still
        // reverse, or close this one and be refused, and the two cases stay distinguishable.
        var today = DateTime.UtcNow.Date;
        currentPeriodStatus ??= PeriodStatus.Open;
        Db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = CurrentPeriodId, FiscalYearId = "fy-2026", PeriodNo = today.Month,
            Name = today.ToString("yyyy-MM"),
            StartDate = new DateTime(today.Year, today.Month, 1),
            EndDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1),
            Status = currentPeriodStatus.Value,
        });

        Db.ChartOfAccounts.AddRange(
            new ChartOfAccount { Id = CashAccountId,    Code = "1100", Name = "Cash",    IsDirectPosting = true },
            new ChartOfAccount { Id = RevenueAccountId, Code = "4000", Name = "Revenue", IsDirectPosting = true },
            new ChartOfAccount { Id = ExpenseAccountId, Code = "5000", Name = "Expense", IsDirectPosting = true },
            // Header/parent accounts exist to group children and must never be posted to directly.
            new ChartOfAccount { Id = HeaderAccountId,  Code = "1000", Name = "Assets",  IsDirectPosting = false },
            // Accounts the AR flow posts to by code rather than id.
            new ChartOfAccount { Id = "acc-ar",       Code = "1200", Name = "Trade Receivables",     IsDirectPosting = true },
            new ChartOfAccount { Id = "acc-ar-govt",  Code = "1201", Name = "Receivables — Govt",    IsDirectPosting = true },
            new ChartOfAccount { Id = "acc-vat-out",  Code = "2200", Name = "VAT Output",            IsDirectPosting = true },
            new ChartOfAccount { Id = "acc-sales",    Code = "4100", Name = "Sales Revenue",         IsDirectPosting = true },
            // Accounts the AP flow posts to.
            new ChartOfAccount { Id = "acc-ap",       Code = "2100", Name = "Trade Payables",         IsDirectPosting = true },
            new ChartOfAccount { Id = "acc-vat-in",   Code = "1230", Name = "VAT Recoverable",        IsDirectPosting = true },
            new ChartOfAccount { Id = "acc-cos",      Code = "5100", Name = "Cost of Sales",          IsDirectPosting = true },
            // Accounts the imprest flow posts to (ImprestRequest's own defaults).
            new ChartOfAccount { Id = "acc-imprest",  Code = "1220", Name = "Staff Imprest",          IsDirectPosting = true },
            new ChartOfAccount { Id = "acc-imprest-exp", Code = "5500", Name = "Imprest Expense",     IsDirectPosting = true }
        );

        Db.Suppliers.Add(new Supplier { Id = SupplierId, Code = "S001", Name = "Parts Co" });

        Db.Customers.Add(new Customer { Id = CustomerId, Code = "C001", Name = "Acme Ltd" });
        Db.Customers.Add(new Customer { Id = GovtCustomerId, Code = "C002", Name = "Ministry", IsGovernment = true });

        Db.TaxCategories.AddRange(
            new TaxCategory { Id = "tax-a", Code = "A", Name = "Standard 16%", Rate = 0.16m },
            new TaxCategory { Id = "tax-e", Code = "E", Name = "Exempt",       Rate = 0m }
        );

        Db.SaveChanges();
        Journals = new JournalService(Db);
        Invoices = new InvoiceService(Db, Journals, new StubEtimsProvider());
        Bills = new SupplierInvoiceService(Db, Journals);
        Receipts = new ReceiptService(Db, Journals);
        // Enforcement off (no Finance:EnforceApprovalAuthority key), matching production default —
        // ApprovalAuthorityService.Ensure is then a no-op, so tests don't need real finance.* roles.
        Vouchers = new PaymentVoucherService(Db, Journals, new ApprovalAuthorityService(new ConfigurationBuilder().Build()));
        Imprests = new ImprestService(Db, Journals, new ApprovalAuthorityService(new ConfigurationBuilder().Build()));
    }

    /// <summary>A balanced two-line journal: debit cash, credit revenue.</summary>
    public static CreateJournalDto BalancedJournal(decimal amount = 1000m, DateTime? date = null) => new()
    {
        EntryDate = date ?? InPeriod,
        Description = "Test entry",
        Lines =
        {
            new CreateJournalLineDto { AccountId = CashAccountId,    Debit = amount, Credit = 0m },
            new CreateJournalLineDto { AccountId = RevenueAccountId, Debit = 0m,     Credit = amount },
        },
    };

    /// <summary>A one-line invoice at the standard 16% rate.</summary>
    public static CreateInvoiceDto Invoice(decimal unitPrice = 1000m, string taxCode = "A",
                                           string? customerId = null, DateTime? date = null) => new()
    {
        CustomerId = customerId ?? CustomerId,
        InvoiceDate = date ?? InPeriod,
        Lines = { new CreateInvoiceLineDto { Description = "Widget", Quantity = 1m, UnitPrice = unitPrice, TaxCode = taxCode } },
    };

    /// <summary>A one-line supplier invoice at the standard 16% rate.</summary>
    /// <summary>A receipt against the seeded customer, auto-allocated oldest-invoice-first.</summary>
    public static CreateReceiptDto Receipt(decimal amount = 400m, DateTime? date = null) => new()
    {
        CustomerId = CustomerId,
        PaymentDate = date ?? InPeriod,
        Amount = amount,
        CurrencyCode = "KES",
        Channel = "Bank",
        ReceiptReference = "RCT-TEST",
    };

    public static CreateSupplierInvoiceDto Bill(decimal unitPrice = 1000m, string taxCode = "A",
                                                DateTime? date = null) => new()
    {
        SupplierId = SupplierId,
        SupplierInvoiceNo = $"SI-{Guid.NewGuid().ToString()[..6]}",
        InvoiceDate = date ?? InPeriod,
        Lines = { new CreateSupplierInvoiceLineDto { Description = "Parts", Quantity = 1m, UnitPrice = unitPrice, TaxCode = taxCode } },
    };

    public void Dispose() => Db.Dispose();
}
