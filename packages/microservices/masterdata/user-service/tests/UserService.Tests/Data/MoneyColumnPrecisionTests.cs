using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Infrastructure.Data;
using Xunit;

namespace UserService.Tests.Data;

/// <summary>
/// #383 — SubscriptionPlan's two money columns previously had no declared precision. Model-level
/// check only (no real connection is opened): Postgres enforces the actual rounding, this just
/// asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static LanteUserServiceDbContext Context() =>
        new(new DbContextOptionsBuilder<LanteUserServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Subscription_plan_prices_have_explicit_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(SubscriptionPlan))!;
        Assert.Equal("numeric(18,2)", entity.FindProperty(nameof(SubscriptionPlan.PriceMonthly))!.GetColumnType());
        Assert.Equal("numeric(18,2)", entity.FindProperty(nameof(SubscriptionPlan.PriceAnnual))!.GetColumnType());
    }
}
