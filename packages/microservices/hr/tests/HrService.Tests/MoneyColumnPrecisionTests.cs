using FluentAssertions;
using HrService.Core.Entities;
using HrService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HrService.Tests;

/// <summary>
/// #383 — CommissionStatement's three money fields and Applicant's interview-score average had no
/// declared precision, unlike every sibling column in the same entities. Model-level checks only
/// (no real connection is opened): Postgres enforces the actual rounding, this just asserts the
/// column type EF will emit.
/// </summary>
public class MoneyColumnPrecisionTests
{
    private static HrDbContext Context() =>
        new(new DbContextOptionsBuilder<HrDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);

    [Fact]
    public void Commission_statement_previously_unconstrained_money_fields_now_have_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(CommissionStatement))!;
        entity.FindProperty(nameof(CommissionStatement.CommissionEarnedToDate))!.GetColumnType().Should().Be("numeric(18,2)");
        entity.FindProperty(nameof(CommissionStatement.PriorCommissionThisYear))!.GetColumnType().Should().Be("numeric(18,2)");
        entity.FindProperty(nameof(CommissionStatement.UnrecoveredOverpayment))!.GetColumnType().Should().Be("numeric(18,2)");
    }

    [Fact]
    public void Applicant_average_interview_score_matches_the_interview_score_precision()
    {
        using var ctx = Context();
        var entity = ctx.Model.FindEntityType(typeof(Applicant))!;
        entity.FindProperty(nameof(Applicant.AverageInterviewScore))!.GetColumnType().Should().Be("numeric(5,2)");
    }
}
