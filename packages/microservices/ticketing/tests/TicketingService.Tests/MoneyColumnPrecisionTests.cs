using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Infrastructure.Data;
using Xunit;

namespace TicketingService.Tests;

/// <summary>
/// #383 — every decimal column defaults to numeric(18,2) unless a naming pattern or an explicit
/// override says otherwise. Model-level checks only (no real connection is opened): Postgres
/// enforces the actual rounding, this just asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static TicketingDbContext Context() =>
        new(new DbContextOptionsBuilder<TicketingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Previously_unconstrained_money_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(Quotation))!;
        entity.FindProperty(nameof(Quotation.TotalAmount))!.GetColumnType().Should().Be("numeric(18,2)");
    }
}
