using ReportingService.Core.Services;
using FluentAssertions;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// #380 — the exact half-cent cases where commercial (AwayFromZero) rounding disagrees with
/// .NET's default banker's rounding (ToEven). Reporting recomputes figures from raw upstream
/// data, so a mismatched mode here is exactly what let a report silently disagree with the
/// screen it summarizes.
/// </summary>
public class MoneyTests
{
    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(2.345, 2.35)]
    [InlineData(1234.565, 1234.57)]
    public void Rounds_half_cents_away_from_zero(decimal value, decimal expected) =>
        Money.Round(value).Should().Be(expected);
}
