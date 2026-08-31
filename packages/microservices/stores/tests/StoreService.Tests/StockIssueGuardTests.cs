using AutoMapper;
using StoreService.Core.DTOs.Issues;
using StoreService.Core.Entities;
using StoreService.Core.Enums;
using StoreService.Core.Interfaces.Repositories;
using StoreService.Core.Mappings;
using StoreService.Core.Services;
using StoreService.Infrastructure.Data;
using StoreService.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace StoreService.Tests;

/// <summary>
/// Guards on issuing stock (#362) — the first tests in store-service.
///
/// <para><b>Scope, stated plainly.</b> These cover the path that takes NO stock unit, because that is
/// where the holes were and because it is the only one this provider can run: the lot path uses
/// <c>ExecuteUpdateAsync</c>, which the in-memory provider does not implement. The lot path's atomic
/// conditional update is a real database property and needs Postgres to test, exactly as
/// depreciation's advisory lock does (#230).</para>
///
/// <para>Two defects, both silent:</para>
/// <list type="number">
/// <item>A negative quantity ran the method <b>backwards</b>. The movement is written as
/// <c>Quantity = -dto.QtyIssued</c> and the total as <c>QtyOnHand -= dto.QtyIssued</c>, so issuing
/// -100 recorded a +100 receipt and raised stock to match — inventory created with no GRN, no
/// supplier and no cost. It cleared every existing guard: <c>-100 &gt; stockUnit.Qty</c> is false, and
/// the atomic <c>WHERE Qty &gt;= -100</c> passes and then INCREASES the lot.</item>
/// <item>Without a lot there was no availability check at all — the entire guard sat inside
/// <c>if (!string.IsNullOrWhiteSpace(dto.StockUnitId))</c>.</item>
/// </list>
/// </summary>
public class StockIssueGuardTests : IDisposable
{
    private const string ItemId = "item-1";
    private const string LocationId = "loc-1";

    private readonly StoreDbContext _db;
    private readonly StockService _sut;

    public StockIssueGuardTests()
    {
        _db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase($"stores-issue-guards-{Guid.NewGuid()}").Options);

        _db.Set<ItemMaster>().Add(new ItemMaster
        {
            Id = ItemId, ItemCode = "WIDGET-1", Description = "Widget", QtyOnHand = 5m,
            MinStockLevel = 1m, IsActive = true,
        });
        _db.Set<Location>().Add(new Location { Id = LocationId, Name = "Main Store" });
        // Five on hand at the location, as a receipt movement — balances are a signed sum of these.
        _db.Set<StockMovement>().Add(new StockMovement
        {
            ItemId = ItemId, LocationId = LocationId, Type = MovementType.Receipt,
            Quantity = 5m, BalanceAfter = 5m, OccurredAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
        _sut = new StockService(
            Repo<ItemMaster>(), Repo<Location>(), Repo<StockUnit>(), Repo<StoreIssueNote>(),
            Repo<SoldItem>(), Repo<StockTakeReconciliation>(), Repo<StockMovement>(),
            Repo<StoreTransfer>(), mapper);
    }

    private IGenericRepository<T> Repo<T>() where T : BaseEntity => new GenericRepository<T>(_db);

    private static CreateStoreIssueNoteDto Issue(decimal qty) => new()
    {
        ItemId = ItemId, LocationId = LocationId, QtyIssued = qty,
        IssueType = "Internal", IssuedTo = "workshop",
    };

    // ── A negative issue must not create stock ──────────────────────────────────

    [Theory]
    [InlineData(-100)]
    [InlineData(-0.5)]
    [InlineData(0)]
    public async Task A_non_positive_issue_is_refused(decimal qty)
    {
        var act = () => _sut.CreateStoreIssueAsync(Issue(qty), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public async Task A_refused_negative_issue_leaves_stock_exactly_as_it_was()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateStoreIssueAsync(Issue(-100m), "tester"));
        _db.ChangeTracker.Clear();

        // Before the guard this recorded a +100 movement and took QtyOnHand from 5 to 105 — an
        // unauthorised receipt with no cost attached to it.
        (await _db.Set<ItemMaster>().SingleAsync(i => i.Id == ItemId)).QtyOnHand.Should().Be(5m);
        (await _db.Set<StockMovement>().CountAsync()).Should().Be(1);
        (await _db.Set<StoreIssueNote>().CountAsync()).Should().Be(0);
    }

    // ── Issuing more than exists ────────────────────────────────────────────────

    [Fact]
    public async Task Issuing_more_than_the_location_holds_is_refused()
    {
        var act = () => _sut.CreateStoreIssueAsync(Issue(1_000m), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public async Task A_refused_over_issue_does_not_drive_the_balance_negative()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateStoreIssueAsync(Issue(1_000m), "tester"));
        _db.ChangeTracker.Clear();

        // Previously QtyOnHand went to -995 and a matching negative movement was written. QtyOnHand
        // is not display-only: PurchasingService alerts on `QtyOnHand <= MinStockLevel`, so a negative
        // pins the item in that list for ever.
        (await _db.Set<ItemMaster>().SingleAsync(i => i.Id == ItemId)).QtyOnHand.Should().Be(5m);
        (await _db.Set<StockMovement>().CountAsync()).Should().Be(1);
    }

    // ── The boundary and the happy path ─────────────────────────────────────────

    [Fact]
    public async Task Issuing_exactly_what_is_on_hand_is_allowed()
    {
        // The boundary a `>=` slip would break, refusing a perfectly ordinary full issue.
        await _sut.CreateStoreIssueAsync(Issue(5m), "tester");
        _db.ChangeTracker.Clear();

        (await _db.Set<ItemMaster>().SingleAsync(i => i.Id == ItemId)).QtyOnHand.Should().Be(0m);
    }

    [Fact]
    public async Task An_ordinary_issue_still_writes_a_negative_movement_and_lowers_the_total()
    {
        await _sut.CreateStoreIssueAsync(Issue(2m), "tester");
        _db.ChangeTracker.Clear();

        (await _db.Set<ItemMaster>().SingleAsync(i => i.Id == ItemId)).QtyOnHand.Should().Be(3m);
        var movement = await _db.Set<StockMovement>().SingleAsync(m => m.Type == MovementType.Issue);
        // Signed: issues are negative, which is what makes the balance a plain sum.
        movement.Quantity.Should().Be(-2m);
        movement.BalanceAfter.Should().Be(3m);
    }

    [Fact]
    public async Task Successive_issues_draw_the_balance_down_and_the_second_is_refused_when_short()
    {
        await _sut.CreateStoreIssueAsync(Issue(4m), "tester");
        _db.ChangeTracker.Clear();

        // Only 1 left, so a 2 must now fail — the check reads the movement ledger rather than a
        // cached figure, so it sees the first issue.
        var act = () => _sut.CreateStoreIssueAsync(Issue(2m), "tester");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient stock*");
    }

    // ── Which source of truth governs ───────────────────────────────────────────

    [Fact]
    public async Task The_movement_ledger_governs_when_it_disagrees_with_the_cached_total()
    {
        // ItemMaster.QtyOnHand is a maintained running total; GetBalancesForItemAsync sums
        // StockMovement.Quantity. Nothing reconciles the two, so once they drift there is no
        // detector — noted on #362. This pins which one is allowed to authorise an issue.
        //
        // The ledger, because it is the auditable record and the cache is derived from it. Trusting
        // the cache would let drift authorise an issue the stock cannot cover, which is the very
        // failure the guard exists to stop.
        var item = await _db.Set<ItemMaster>().SingleAsync(i => i.Id == ItemId);
        item.QtyOnHand = 100m;          // cache says plenty
        await _db.SaveChangesAsync();   // ledger still sums to 5
        _db.ChangeTracker.Clear();

        var act = () => _sut.CreateStoreIssueAsync(Issue(10m), "tester");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    public void Dispose() => _db.Dispose();
}
