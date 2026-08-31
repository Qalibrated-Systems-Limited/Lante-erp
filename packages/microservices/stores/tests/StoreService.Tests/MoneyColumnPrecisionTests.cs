using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StoreService.Core.Entities;
using StoreService.Infrastructure.Data;
using Xunit;

namespace StoreService.Tests;

/// <summary>
/// #383 — GoodsReceivedNote.AcceptedQty/RejectedQty and GoodsRejectionNote.RejectedQty had no
/// declared precision, unlike every sibling quantity/cost column in the same entities. Model-level
/// checks only (no real connection is opened): Postgres enforces the actual rounding, this just
/// asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static StoreDbContext Context() =>
        new(new DbContextOptionsBuilder<StoreDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Goods_received_note_previously_unconstrained_quantities_now_have_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(GoodsReceivedNote))!;
        entity.FindProperty(nameof(GoodsReceivedNote.AcceptedQty))!.GetColumnType().Should().Be("numeric(18,2)");
        entity.FindProperty(nameof(GoodsReceivedNote.RejectedQty))!.GetColumnType().Should().Be("numeric(18,2)");
    }

    [Fact]
    public void Goods_rejection_note_quantity_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(GoodsRejectionNote))!;
        entity.FindProperty(nameof(GoodsRejectionNote.RejectedQty))!.GetColumnType().Should().Be("numeric(18,2)");
    }
}
