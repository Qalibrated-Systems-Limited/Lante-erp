using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.Entities;
using OperationsService.Infrastructure.Data;
using Xunit;

namespace OperationsService.Tests;

/// <summary>
/// #383 — every decimal column defaults to numeric(18,2) unless a naming pattern or an explicit
/// override says otherwise. Model-level checks only (no real connection is opened): Postgres
/// enforces the actual rounding, SQLite doesn't, so these assert the column type EF will emit,
/// not runtime rounding behavior.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static OperationsDbContext Context() =>
        new(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Previously_unconstrained_money_column_now_has_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(Requisition))!;
        entity.FindProperty(nameof(Requisition.ApprovedAmount))!.GetColumnType().Should().Be("numeric(18,2)");
    }

    [Fact]
    public void Percentage_column_gets_finer_precision_than_plain_money()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(ProjectTemplateMilestone))!;
        entity.FindProperty(nameof(ProjectTemplateMilestone.ValuePct))!.GetColumnType().Should().Be("numeric(9,4)");
    }

    [Fact]
    public void Already_tuned_precision_is_not_overwritten_by_the_backstop_default()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(Project))!;
        // Explicitly configured further down OnModelCreating as decimal(6,4) - a fractional daily
        // liquidated-damages rate, not money - must win over the blanket numeric(18,2) default.
        entity.FindProperty(nameof(Project.LdRatePerDay))!.GetColumnType().Should().Be("numeric(6,4)");
    }
}
