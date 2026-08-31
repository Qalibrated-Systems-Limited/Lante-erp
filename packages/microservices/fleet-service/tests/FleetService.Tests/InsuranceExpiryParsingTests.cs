using FluentAssertions;
using FleetService.Core.Services;
using Xunit;

namespace FleetService.Tests;

/// <summary>
/// FieldVehicleService.TryParseInsuranceExpiry (#375) — parses the same formats the
/// AddFieldVehicleInsuranceExpiryDate migration's backfill accepts (ISO, dd/mm/yyyy, dd.mm.yyyy),
/// so a value written today parses identically to how a legacy row would have been backfilled.
/// This is what keeps FieldVehicle.InsuranceExpiryDate — the field VehicleExpiryBackgroundService
/// actually reads — populated for new writes, not just a one-time migration backfill.
/// </summary>
public class InsuranceExpiryParsingTests
{
    [Theory]
    [InlineData("2026-08-27", 2026, 8, 27)]  // ISO — what <input type="date"> actually emits
    [InlineData("27/08/2026", 2026, 8, 27)]  // dd/mm/yyyy — what a Kenyan user types by hand
    [InlineData("27.08.2026", 2026, 8, 27)]  // dd.mm.yyyy
    public void Parses_each_accepted_format(string raw, int year, int month, int day)
        => FieldVehicleService.TryParseInsuranceExpiry(raw).Should().Be(new DateTime(year, month, day));

    [Theory]
    [InlineData("expired")]
    [InlineData("n/a")]
    [InlineData("31/02/2026")]   // regex-shaped but not a real calendar date
    [InlineData("2026-13-01")]   // invalid month
    [InlineData("08/27/2026")]   // mm/dd/yyyy — not one of the accepted formats, ambiguous with dd/mm
    public void Unparseable_or_out_of_format_values_return_null_rather_than_throw(string raw)
        => FieldVehicleService.TryParseInsuranceExpiry(raw).Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_input_returns_null(string? raw)
        => FieldVehicleService.TryParseInsuranceExpiry(raw).Should().BeNull();

    [Fact]
    public void Leading_and_trailing_whitespace_around_an_otherwise_valid_value_is_tolerated()
        => FieldVehicleService.TryParseInsuranceExpiry("  2026-08-27  ").Should().Be(new DateTime(2026, 8, 27));
}
