using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReportingService.Core.Entities;
using ReportingService.Infrastructure.Data;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// #383 — every decimal column defaults to numeric(18,2) unless a naming pattern or an explicit
/// override says otherwise. Model-level checks only (no real connection is opened): Postgres
/// enforces the actual rounding, this just asserts the column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static ReportingDbContext Context() =>
        new(new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Previously_unconstrained_threshold_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(KpiScorecard))!;
        entity.FindProperty(nameof(KpiScorecard.TargetValue))!.GetColumnType().Should().Be("numeric(18,2)");
    }
}
