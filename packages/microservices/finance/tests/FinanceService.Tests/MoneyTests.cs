using FinanceService.Core.Services;
using FluentAssertions;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// #380 — the exact half-cent cases where commercial (AwayFromZero) rounding disagrees with
/// .NET's default banker's rounding (ToEven). Percentage arithmetic (VAT, discounts) produces
/// exactly these values, so a mismatch here is what let hr's payroll journal disagree with
/// finance's own trial balance by a cent.
/// </summary>
public class MoneyTests
{
    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(2.345, 2.35)]
    [InlineData(1234.565, 1234.57)]
    public void Rounds_half_cents_away_from_zero(decimal value, decimal expected) =>
        Money.Round(value).Should().Be(expected);

    [Fact]
    public void Disagrees_with_bare_Math_Round_at_the_cases_that_matter()
    {
        // Pins the actual defect #380 describes: .NET's default (ToEven) gives a DIFFERENT
        // answer than Money.Round at an exact half-cent. If this ever starts passing, Money.Round
        // has silently regressed to ToEven.
        Math.Round(0.125m, 2).Should().NotBe(Money.Round(0.125m));
        Math.Round(2.345m, 2).Should().NotBe(Money.Round(2.345m));
    }
}
