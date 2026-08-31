using FluentAssertions;
using LicenseService.Infrastructure.BackgroundServices;
using Xunit;

namespace LicenseService.Tests;

/// <summary>
/// The expiry band used in the alert title, which is the alert dedup KEY.
///
/// <para>Ticketing dedupes alerts on (TenantId, Source, Title). The title used to embed the raw day count,
/// which made the key time-dependent — and each pod runs its own <c>Task.Delay(24h)</c> loop started 30
/// seconds after boot, so replicas hold a PERMANENT offset. Any licence whose day boundary falls inside
/// that offset computed 30 on one replica and 29 on the other: different title, different key, two alerts
/// and two notifications, every day (#219).</para>
///
/// <para>These tests are about that stability, not about the wording. The band must be identical for two
/// replicas whose clocks differ by minutes, and must still escalate as the date approaches — a single
/// constant title would dedupe perfectly and never tell anyone the date got closer.</para>
/// </summary>
public class ExpiryBandTests
{
    [Theory]
    [InlineData(30, 29)]     // the boundary that actually bit: replicas either side of a day tick
    [InlineData(29, 25)]
    [InlineData(14, 8)]
    [InlineData(7, 2)]
    [InlineData(0, -5)]      // expired, however long ago
    public void Two_replicas_disagreeing_about_the_day_count_still_agree_on_the_band(int a, int b)
        => LicenseExpiryBackgroundService.ExpiryBand(a)
            .Should().Be(LicenseExpiryBackgroundService.ExpiryBand(b));

    [Fact]
    public void The_band_still_escalates_as_the_date_approaches()
    {
        var bands = new[] { 30, 14, 7, 1, 0 }
            .Select(LicenseExpiryBackgroundService.ExpiryBand)
            .ToList();

        // A single constant title would dedupe perfectly and never escalate. Distinct bands are what keep
        // a fresh, louder alert appearing as the date closes in.
        bands.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(0, "expired")]
    [InlineData(-1, "expired")]
    [InlineData(-400, "expired")]
    public void Anything_already_expired_lands_in_one_band(int daysLeft, string expected)
        // Otherwise a licence three months overdue would raise a new alert every day forever, each with a
        // different title and so a different dedup key.
        => LicenseExpiryBackgroundService.ExpiryBand(daysLeft).Should().Be(expected);

    [Theory]
    [InlineData(1, "1 day")]
    [InlineData(7, "7 days")]
    [InlineData(14, "14 days")]
    [InlineData(15, "30 days")]
    [InlineData(30, "30 days")]
    [InlineData(365, "30 days")]
    public void The_bands_are_the_documented_thresholds(int daysLeft, string expected)
        => LicenseExpiryBackgroundService.ExpiryBand(daysLeft).Should().Be(expected);

    [Fact]
    public void A_band_boundary_belongs_to_the_tighter_band()
        // <= rather than <, so exactly 7 days out is the "7 days" warning and not the "14 days" one.
        // Getting this inverted would delay every escalation by one band.
        => LicenseExpiryBackgroundService.ExpiryBand(7).Should().Be("7 days");
}
