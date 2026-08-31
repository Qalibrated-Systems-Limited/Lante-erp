using FleetService.Core.Entities;
using FleetService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetService.Tests;

/// <summary>
/// #383 — every decimal column defaults to numeric(18,2) unless a naming pattern or an explicit
/// override says otherwise. Model-level checks only (no real connection is opened): Postgres
/// enforces the actual rounding, this just asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static FleetServiceDbContext Context() =>
        new(new DbContextOptionsBuilder<FleetServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Previously_unconstrained_money_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(Trip))!;
        entity.FindProperty(nameof(Trip.Revenue))!.GetColumnType().Should().Be("numeric(18,2)");
    }

    [Fact]
    public void Previously_unconstrained_odometer_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(Truck))!;
        entity.FindProperty(nameof(Truck.Odometer))!.GetColumnType().Should().Be("numeric(18,2)");
    }
}
