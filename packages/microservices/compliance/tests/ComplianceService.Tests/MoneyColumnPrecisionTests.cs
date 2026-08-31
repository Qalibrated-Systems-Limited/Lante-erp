using ComplianceService.Core.Entities;
using ComplianceService.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComplianceService.Tests;

/// <summary>
/// #383 — every decimal column defaults to numeric(18,2) unless a naming pattern or an explicit
/// override says otherwise. Model-level checks only (no real connection is opened): Postgres
/// enforces the actual rounding, this just asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static ComplianceDbContext Context() =>
        new(new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Previously_unconstrained_money_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(RelatedPartyTransaction))!;
        entity.FindProperty(nameof(RelatedPartyTransaction.Amount))!.GetColumnType().Should().Be("numeric(18,2)");
    }
}
