using FluentAssertions;
using FleetService.Core.Entities;
using FleetService.Infrastructure.BackgroundServices;
using Xunit;

namespace FleetService.Tests;

/// <summary>
/// Pure classification logic for VehicleExpiryBackgroundService (#375) — driver licences had a
/// banded-alert sweep; nothing equivalent ever read vehicle insurance/inspection expiry, so a
/// vehicle could be dispatched on lapsed cover with no alert at all. These tests cover the
/// decision rules (what counts as a finding, what gets excluded, how an unreadable legacy
/// insurance string is treated) without touching a database — the actual DB round-trip is a
/// deliberately dumb "!IsDeleted" fetch, all real logic lives in these pure functions.
/// </summary>
public class VehicleExpiryFindingsTests
{
    private static readonly DateTime WarningDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Truck NewTruck(TruckStatus status = TruckStatus.Active, DateTime? insurance = null, DateTime? inspection = null) => new()
    {
        LicensePlate = "KDA 123X",
        Status = status,
        InsuranceExpiryDate = insurance,
        InspectionExpiryDate = inspection,
    };

    private static FieldVehicle NewFieldVehicle(
        FieldVehicleStatus status = FieldVehicleStatus.Available,
        DateTime? insuranceDate = null, string? insuranceRaw = null, DateTime? inspection = null) => new()
    {
        RegistrationNumber = "KDB 456Y",
        Status = status,
        InsuranceExpiryDate = insuranceDate,
        InsuranceExpiry = insuranceRaw,
        InspectionExpiryDate = inspection,
    };

    // ── Truck ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Truck_with_insurance_expiring_within_window_is_a_finding()
    {
        var truck = NewTruck(insurance: WarningDate.AddDays(-5));
        VehicleExpiryBackgroundService.FindingsForTruck(truck, WarningDate)
            .Should().ContainSingle(f => f.Kind == VehicleExpiryBackgroundService.FindingKind.Insurance);
    }

    [Fact]
    public void Truck_with_inspection_expiring_within_window_is_a_finding()
    {
        var truck = NewTruck(inspection: WarningDate.AddDays(-1));
        VehicleExpiryBackgroundService.FindingsForTruck(truck, WarningDate)
            .Should().ContainSingle(f => f.Kind == VehicleExpiryBackgroundService.FindingKind.Inspection);
    }

    [Fact]
    public void Truck_with_both_expiring_raises_both_findings()
    {
        var truck = NewTruck(insurance: WarningDate.AddDays(-5), inspection: WarningDate.AddDays(-1));
        VehicleExpiryBackgroundService.FindingsForTruck(truck, WarningDate).Should().HaveCount(2);
    }

    [Fact]
    public void Truck_with_dates_well_outside_the_window_is_not_a_finding()
    {
        var truck = NewTruck(insurance: WarningDate.AddYears(1), inspection: WarningDate.AddYears(1));
        VehicleExpiryBackgroundService.FindingsForTruck(truck, WarningDate).Should().BeEmpty();
    }

    [Fact]
    public void Truck_with_no_dates_set_is_not_a_finding()
    {
        VehicleExpiryBackgroundService.FindingsForTruck(NewTruck(), WarningDate).Should().BeEmpty();
    }

    [Theory]
    [InlineData(TruckStatus.Decommissioned)]
    [InlineData(TruckStatus.OutOfService)]
    public void A_decommissioned_or_out_of_service_truck_is_not_eligible_even_with_lapsed_cover(TruckStatus status)
        // Not dispatched, so a lapsed date on it carries none of the legal exposure a truck
        // actually on the road would — alerting on it would just be noise nobody can act on.
        => VehicleExpiryBackgroundService.IsEligible(NewTruck(status)).Should().BeFalse();

    [Theory]
    [InlineData(TruckStatus.Active)]
    [InlineData(TruckStatus.InMaintenance)]
    public void An_active_or_in_maintenance_truck_is_eligible(TruckStatus status)
        => VehicleExpiryBackgroundService.IsEligible(NewTruck(status)).Should().BeTrue();

    // ── FieldVehicle ───────────────────────────────────────────────────────────

    [Fact]
    public void FieldVehicle_with_parsed_insurance_date_expiring_is_a_finding()
    {
        var vehicle = NewFieldVehicle(insuranceDate: WarningDate.AddDays(-5));
        VehicleExpiryBackgroundService.FindingsForFieldVehicle(vehicle, WarningDate)
            .Should().ContainSingle(f => f.Kind == VehicleExpiryBackgroundService.FindingKind.Insurance);
    }

    [Fact]
    public void FieldVehicle_with_unparseable_legacy_string_and_no_parsed_date_is_flagged_unreadable_not_expired_or_compliant()
    {
        // The backfill migration couldn't parse "expired" (or any other garbage) into a real
        // date, so InsuranceExpiryDate is null — this must surface as its own state, not
        // silently read as compliant (nothing raised) or silently read as expired (a claim the
        // data doesn't support). Mirrors expiry.js's 'unknown' state from #376.
        var vehicle = NewFieldVehicle(insuranceRaw: "expired");
        var findings = VehicleExpiryBackgroundService.FindingsForFieldVehicle(vehicle, WarningDate).ToList();

        findings.Should().ContainSingle();
        findings[0].Kind.Should().Be(VehicleExpiryBackgroundService.FindingKind.InsuranceUnreadable);
    }

    [Fact]
    public void FieldVehicle_with_no_insurance_value_at_all_is_not_a_finding()
        // Genuinely unset is not a problem the way "present but unreadable" is.
        => VehicleExpiryBackgroundService.FindingsForFieldVehicle(NewFieldVehicle(), WarningDate).Should().BeEmpty();

    [Fact]
    public void FieldVehicle_with_whitespace_only_legacy_string_is_treated_as_unset_not_unreadable()
        => VehicleExpiryBackgroundService.FindingsForFieldVehicle(NewFieldVehicle(insuranceRaw: "   "), WarningDate).Should().BeEmpty();

    [Fact]
    public void FieldVehicle_with_a_parsed_date_never_also_raises_unreadable_even_if_the_raw_string_is_present()
    {
        // Once the backfill successfully parsed a date, the original raw string (kept for
        // display/audit) must not cause a second, contradictory alert.
        var vehicle = NewFieldVehicle(insuranceDate: WarningDate.AddDays(-5), insuranceRaw: "2026-08-27");
        VehicleExpiryBackgroundService.FindingsForFieldVehicle(vehicle, WarningDate)
            .Should().ContainSingle(f => f.Kind == VehicleExpiryBackgroundService.FindingKind.Insurance);
    }

    [Fact]
    public void FieldVehicle_with_inspection_expiring_is_a_finding()
    {
        var vehicle = NewFieldVehicle(inspection: WarningDate.AddDays(-1));
        VehicleExpiryBackgroundService.FindingsForFieldVehicle(vehicle, WarningDate)
            .Should().ContainSingle(f => f.Kind == VehicleExpiryBackgroundService.FindingKind.Inspection);
    }

    [Fact]
    public void A_decommissioned_field_vehicle_is_not_eligible_even_with_lapsed_cover()
        => VehicleExpiryBackgroundService.IsEligible(NewFieldVehicle(FieldVehicleStatus.Decommissioned)).Should().BeFalse();

    [Theory]
    [InlineData(FieldVehicleStatus.Available)]
    [InlineData(FieldVehicleStatus.Dispatched)]
    [InlineData(FieldVehicleStatus.UnderMaintenance)]
    public void A_non_decommissioned_field_vehicle_is_eligible(FieldVehicleStatus status)
        => VehicleExpiryBackgroundService.IsEligible(NewFieldVehicle(status)).Should().BeTrue();

    // ── Boundary ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_date_exactly_on_the_warning_boundary_is_included()
        // <=, matching LicenseExpiryBackgroundService's own boundary convention.
        => VehicleExpiryBackgroundService.FindingsForTruck(NewTruck(insurance: WarningDate), WarningDate)
            .Should().ContainSingle();

    [Fact]
    public void A_date_one_tick_past_the_warning_boundary_is_excluded()
        => VehicleExpiryBackgroundService.FindingsForTruck(NewTruck(insurance: WarningDate.AddTicks(1)), WarningDate)
            .Should().BeEmpty();
}
