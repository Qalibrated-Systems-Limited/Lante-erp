using FleetService.Core.Entities;
using FluentAssertions;
using Xunit;

namespace FleetService.Tests;

/// <summary>
/// Mileage-based service scheduling on <see cref="FieldVehicle"/> and <see cref="Truck"/>.
///
/// <para>This computation is <b>duplicated</b>. Both entities carry their own
/// <c>NextServiceOdometer</c> and <c>IsServiceDueByMileage</c>, identical but for the odometer
/// field name (<c>CurrentOdometer</c> against <c>Odometer</c>). They agree today; nothing keeps them
/// agreeing, and fleet-service had five tests in total, all of them on <c>ExpiryBand</c>.</para>
///
/// <para>Two copies of one rule drifting is #204's shape, and the consequence here is a vehicle that
/// one screen calls due for service and another calls fine. So the last test in this file drives
/// <b>both</b> entities through the same table and asserts they answer identically — the copies can
/// still diverge, but not silently.</para>
///
/// <para>Why it matters that mileage is the deciding signal: a heavily used vehicle blows past its
/// service interval in weeks while the calendar date still looks comfortable. Missing that is a
/// mechanical failure on the road, not a paperwork problem.</para>
/// </summary>
public class VehicleServiceDueTests
{
    private static FieldVehicle Vehicle(decimal current, decimal? lastService, decimal? interval) => new()
    {
        CurrentOdometer = current, LastServiceOdometer = lastService, ServiceIntervalKm = interval,
    };

    private static Truck Lorry(decimal current, decimal? lastService, decimal? interval) => new()
    {
        Odometer = current, LastServiceOdometer = lastService, ServiceIntervalKm = interval,
    };

    // ── When mileage tracking is not configured ─────────────────────────────────

    [Fact]
    public void No_service_interval_means_no_mileage_target()
    {
        // Null is the documented "not configured yet" case: existing vehicles must not suddenly read
        // as due with no data behind the claim.
        Vehicle(120_000m, 100_000m, null).NextServiceOdometer.Should().BeNull();
        Vehicle(120_000m, 100_000m, null).IsServiceDueByMileage.Should().BeFalse();
    }

    [Fact]
    public void No_last_service_reading_means_no_mileage_target()
    {
        // An interval with nothing to count from cannot produce a target. Treating a missing last
        // service as zero would make every vehicle overdue the moment an interval was configured.
        //
        // Note for anyone mutation-testing this: dropping `LastServiceOdometer.HasValue` from the
        // guard does NOT break this test, and no test can break it. C#'s lifted operators already
        // propagate null through `LastServiceOdometer + ServiceIntervalKm`, so that half of the
        // condition is redundant rather than load-bearing — the mutant is equivalent, not uncaught.
        // Recorded instead of contorting a test to chase it, and the redundant check is left in
        // place because it states the intent more plainly than nullable arithmetic does.
        Vehicle(120_000m, null, 10_000m).NextServiceOdometer.Should().BeNull();
        Vehicle(120_000m, null, 10_000m).IsServiceDueByMileage.Should().BeFalse();
    }

    // ── The target ──────────────────────────────────────────────────────────────

    [Fact]
    public void The_target_is_the_last_service_plus_the_interval()
    {
        Vehicle(0m, 100_000m, 10_000m).NextServiceOdometer.Should().Be(110_000m);
    }

    [Fact]
    public void The_target_counts_from_the_last_service_not_from_zero()
    {
        // A vehicle serviced at 250,000 km with a 15,000 interval is due at 265,000 — not at 15,000,
        // which it passed years ago and which would report every older vehicle as permanently due.
        Vehicle(0m, 250_000m, 15_000m).NextServiceOdometer.Should().Be(265_000m);
    }

    // ── Due or not ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_vehicle_short_of_its_target_is_not_due()
    {
        Vehicle(109_999m, 100_000m, 10_000m).IsServiceDueByMileage.Should().BeFalse();
    }

    [Fact]
    public void A_vehicle_exactly_on_its_target_is_due()
    {
        // The boundary, and the one that matters: an interval is a limit, not a suggestion, so
        // reaching it counts. A `>` here would let a vehicle run indefinitely at exactly its target.
        Vehicle(110_000m, 100_000m, 10_000m).IsServiceDueByMileage.Should().BeTrue();
    }

    [Fact]
    public void A_vehicle_past_its_target_is_due()
    {
        Vehicle(150_000m, 100_000m, 10_000m).IsServiceDueByMileage.Should().BeTrue();
    }

    [Fact]
    public void An_odometer_reading_behind_the_last_service_does_not_report_due()
    {
        // Reachable: a replaced instrument cluster, or a correction keyed against the wrong vehicle.
        // It should read as not-due rather than throwing or flipping — the reading is wrong, and the
        // service schedule is not the place to surface that.
        Vehicle(90_000m, 100_000m, 10_000m).IsServiceDueByMileage.Should().BeFalse();
    }

    [Fact]
    public void A_zero_interval_makes_the_target_the_last_service_itself()
    {
        // Nonsense configuration, but nothing validates ServiceIntervalKm. Worth pinning what it
        // does rather than discovering it: the vehicle reads as permanently due, which is at least
        // loud rather than silently ignored.
        var v = Vehicle(100_000m, 100_000m, 0m);
        v.NextServiceOdometer.Should().Be(100_000m);
        v.IsServiceDueByMileage.Should().BeTrue();
    }

    // ── The two copies must agree ───────────────────────────────────────────────

    [Theory]
    [InlineData(109_999, 100_000, 10_000, false)]
    [InlineData(110_000, 100_000, 10_000, true)]
    [InlineData(150_000, 100_000, 10_000, true)]
    [InlineData(90_000, 100_000, 10_000, false)]
    [InlineData(0, 0, 1, false)]
    public void FieldVehicle_and_Truck_answer_identically(
        decimal current, decimal lastService, decimal interval, bool expectedDue)
    {
        var vehicle = Vehicle(current, lastService, interval);
        var lorry = Lorry(current, lastService, interval);

        // The point of this test is not the expected value — the tests above cover that. It is that
        // the two independent copies of this rule give the SAME answer, so a change to one without
        // the other fails here instead of surfacing as two screens disagreeing about one vehicle.
        vehicle.NextServiceOdometer.Should().Be(lorry.NextServiceOdometer);
        vehicle.IsServiceDueByMileage.Should().Be(lorry.IsServiceDueByMileage);
        vehicle.IsServiceDueByMileage.Should().Be(expectedDue);
    }

    [Fact]
    public void The_two_copies_agree_when_mileage_tracking_is_unconfigured()
    {
        // The null paths too, which the theory above cannot express with decimal InlineData.
        Vehicle(120_000m, 100_000m, null).NextServiceOdometer
            .Should().Be(Lorry(120_000m, 100_000m, null).NextServiceOdometer);
        Vehicle(120_000m, null, 10_000m).IsServiceDueByMileage
            .Should().Be(Lorry(120_000m, null, 10_000m).IsServiceDueByMileage);
    }
}
