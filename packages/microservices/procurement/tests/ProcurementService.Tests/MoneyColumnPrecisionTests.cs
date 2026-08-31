using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.Entities;
using ProcurementService.Infrastructure.Data;
using Xunit;

namespace ProcurementService.Tests;

/// <summary>
/// #383 — every decimal column defaults to numeric(18,2) unless a naming pattern or an explicit
/// override says otherwise. Model-level checks only (no real connection is opened): Postgres
/// enforces the actual rounding, this just asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static ProcurementDbContext Context() =>
        new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Previously_unconstrained_quantity_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(PurchaseOrder))!;
        entity.FindProperty(nameof(PurchaseOrder.ReceivedQty))!.GetColumnType().Should().Be("numeric(18,2)");
    }

    [Fact]
    public void Exchange_rate_keeps_its_finer_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(LandedCostComponent))!;
        entity.FindProperty(nameof(LandedCostComponent.ExchangeRate))!.GetColumnType().Should().Be("numeric(18,6)");
    }
}
