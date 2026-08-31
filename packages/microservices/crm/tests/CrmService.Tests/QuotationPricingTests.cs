using AutoMapper;
using CrmService.Core.DTOs.Quotations;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Mappings;
using CrmService.Core.Services;
using CrmService.Infrastructure.Data;
using CrmService.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrmService.Tests;

/// <summary>
/// Quotation pricing and the MD approval threshold.
///
/// <para>These figures go on a document a customer receives and, once accepted, become the basis of
/// an invoice. <c>QuotationService</c> is 336 lines and had no tests; crm's thirteen were all on the
/// audit interceptor and duplicate-customer detection.</para>
///
/// <para>SQLite rather than InMemory, matching <c>CustomerFixture</c>'s reasoning: this exercises the
/// real repositories and the real AutoMapper profile, and a provider that never builds SQL would
/// assert less than it appears to.</para>
/// </summary>
public class QuotationPricingTests : IDisposable
{
    private const string Actor = "sales-1";

    private readonly SqliteConnection _conn;
    private readonly CrmDbContext _db;
    private readonly QuotationService _sut;
    private readonly string _quoteId;

    public QuotationPricingTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();

        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
        _sut = new QuotationService(
            Repo<Quotation>(), Repo<QuotationLine>(), Repo<Opportunity>(),
            Repo<PriceList>(), Repo<PriceExceptionLog>(), mapper);

        var quote = new Quotation
        {
            QuoteNumber = "QT-2026-0001", Title = "Test quote", OpportunityId = "opp-1",
            CustomerId = "cust-1", CustomerName = "Acme", Status = QuotationStatus.Draft,
            VatRate = 0.16m, CreatedBy = Actor, UpdatedBy = Actor,
        };
        _db.Quotations.Add(quote);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        _quoteId = quote.Id;
    }

    private IGenericRepository<T> Repo<T>() where T : class => new GenericRepository<T>(_db);

    private Task<QuotationDetailDto> SaveLines(params (decimal Qty, decimal Price, decimal DiscountPct)[] lines) =>
        _sut.SaveLinesAsync(_quoteId, new SaveQuotationLinesDto
        {
            Lines = lines.Select((l, i) => new QuotationLineInputDto
            {
                Description = $"Item {i + 1}", Quantity = l.Qty, UnitPrice = l.Price,
                DiscountPercent = l.DiscountPct,
            }).ToList(),
        }, Actor);

    private static void IsMoney(decimal value, string what) =>
        value.Should().Be(Math.Round(value, 2), $"{what} lands on a customer document and must be in cents");

    // ── Sub-cent money ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2.5, 10.99)]        // half-unit pricing — litres, metres, hours
    [InlineData(1.5, 3333.33)]
    [InlineData(3, 33.333)]
    [InlineData(0.25, 19.99)]
    public async Task A_fractional_quantity_does_not_produce_sub_cent_money(decimal qty, decimal price)
    {
        var q = await SaveLines((qty, price, 0m));

        // `gross` is Quantity * UnitPrice with neither operand constrained and the money columns
        // carrying no precision, so before the fix 2.5 @ 10.99 stored a line of 27.475 and a
        // quotation total of 31.875 — sub-cent figures on a document a customer receives and an
        // invoice is later raised from.
        IsMoney(q.Subtotal, "Subtotal");
        IsMoney(q.VatAmount, "VatAmount");
        IsMoney(q.TotalAmount, "TotalAmount");
        foreach (var line in q.Lines) IsMoney(line.LineTotal, "LineTotal");
    }

    [Fact]
    public async Task The_worked_example_that_was_wrong()
    {
        var q = await SaveLines((2.5m, 10.99m, 0m));

        // 2.5 x 10.99 = 27.475, rounded to 27.48. VAT 16% of 27.48 = 4.40. Total 31.88.
        // Previously: line 27.475, total 31.875.
        q.Subtotal.Should().Be(27.48m);
        q.VatAmount.Should().Be(4.40m);
        q.TotalAmount.Should().Be(31.88m);
    }

    [Fact]
    public async Task Sub_cent_precision_does_not_accumulate_across_many_lines()
    {
        // Ten lines each individually sub-cent. Unrounded, the error compounds into the subtotal
        // rather than staying within a cent of it.
        var lines = Enumerable.Repeat((2.5m, 10.99m, 0m), 10).ToArray();

        var q = await SaveLines(lines);

        IsMoney(q.Subtotal, "Subtotal");
        q.Subtotal.Should().Be(274.80m);
        IsMoney(q.TotalAmount, "TotalAmount");
    }

    // ── The arithmetic itself ───────────────────────────────────────────────────

    [Fact]
    public async Task VAT_is_charged_on_the_discounted_subtotal_not_the_gross()
    {
        var q = await SaveLines((10m, 1_000m, 10m));

        // Gross 10,000, 10% discount = 1,000, subtotal 9,000, VAT 1,440, total 10,440.
        // VAT on the gross would be 1,600 — charging the customer tax on money they never owed.
        q.DiscountAmount.Should().Be(1_000m);
        q.Subtotal.Should().Be(9_000m);
        q.VatAmount.Should().Be(1_440m);
        q.TotalAmount.Should().Be(10_440m);
    }

    [Fact]
    public async Task The_subtotal_is_net_of_discount_and_the_discount_is_reported_separately()
    {
        var q = await SaveLines((1m, 500m, 20m), (2m, 250m, 0m));

        // Line 1: 500 less 100 = 400. Line 2: 500. Subtotal 900, discount 100.
        q.Subtotal.Should().Be(900m);
        q.DiscountAmount.Should().Be(100m);
    }

    [Fact]
    public async Task A_hundred_percent_discount_zeroes_the_line_without_going_negative()
    {
        var q = await SaveLines((1m, 500m, 100m));

        q.Subtotal.Should().Be(0m);
        q.VatAmount.Should().Be(0m);
        q.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Replacing_the_lines_replaces_the_totals_rather_than_adding_to_them()
    {
        await SaveLines((1m, 1_000m, 0m));

        var q = await SaveLines((1m, 400m, 0m));

        // SaveLines soft-deletes the previous lines. A subtotal of 1,400 here would mean the old
        // lines were still counted, and the quote would grow every time it was edited.
        q.Subtotal.Should().Be(400m);
    }

    // ── The MD approval threshold ───────────────────────────────────────────────

    private async Task<QuotationStatus> StatusAfterDeptHeadApproval()
    {
        await _sut.SubmitAsync(_quoteId, "sales-1");
        await _sut.DeptHeadReviewAsync(_quoteId, approve: true, reason: null, "dept-head-1");
        _db.ChangeTracker.Clear();
        return (await _db.Quotations.SingleAsync(q => q.Id == _quoteId)).Status;
    }

    [Fact]
    public async Task A_quote_over_the_threshold_goes_to_the_MD()
    {
        await SaveLines((1m, 600_000m, 0m));

        (await StatusAfterDeptHeadApproval()).Should().Be(QuotationStatus.PendingMd);
    }

    [Fact]
    public async Task A_quote_under_the_threshold_is_approved_outright()
    {
        await SaveLines((1m, 100_000m, 0m));

        (await StatusAfterDeptHeadApproval()).Should().Be(QuotationStatus.Approved);
    }

    [Fact]
    public async Task The_threshold_is_measured_on_the_VAT_INCLUSIVE_total()
    {
        // 440,000 of goods is under 500k; with 16% VAT the total is 510,400, which is over. So this
        // needs the MD.
        //
        // Pinned as an OBSERVATION. "MD approves quotes above KES 500k" reads naturally as the quote
        // value, and measuring the VAT-inclusive figure means MD approval actually starts at about
        // 431,000 of goods. Defensible either way — VAT is real money leaving the customer — but it
        // is a policy choice nobody wrote down, and this is where it would change.
        await SaveLines((1m, 440_000m, 0m));

        (await StatusAfterDeptHeadApproval()).Should().Be(QuotationStatus.PendingMd);
    }

    [Fact]
    public async Task Exactly_on_the_threshold_does_not_need_the_MD()
    {
        // The boundary: the rule is "above 500k". A quote totalling exactly 500,000 is approved by
        // the department head. Priced backwards from the total so VAT lands it exactly on the line.
        await SaveLines((1m, 431_034.48m, 0m));
        _db.ChangeTracker.Clear();
        var total = (await _db.Quotations.SingleAsync(q => q.Id == _quoteId)).TotalAmount;
        total.Should().BeLessThanOrEqualTo(500_000m, "the fixture must sit on or under the line");

        (await StatusAfterDeptHeadApproval()).Should().Be(QuotationStatus.Approved);
    }

    // ── Editing after submission ────────────────────────────────────────────────

    [Fact]
    public async Task Lines_cannot_be_changed_once_the_quote_leaves_draft()
    {
        await SaveLines((1m, 100_000m, 0m));
        await _sut.SubmitAsync(_quoteId, "sales-1");

        var act = () => SaveLines((1m, 900_000m, 0m));

        // The guard that makes the threshold meaningful. Without it, a quote could be submitted at
        // 100k, approved by the department head, and then re-priced to 900k — the MD gate bypassed
        // entirely because the check reads the amount stored at approval time.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only a draft quotation can be edited*");
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
