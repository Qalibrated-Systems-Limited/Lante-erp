namespace FleetService.Core.Entities;

// Backed by the live "Vehicles" table (see FleetServiceDbContext.OnModelCreating) — a prior
// gap-fill PR (#136) renamed Trucks -> Vehicles and added the master-data columns below on the
// production DB, then was reverted at the application-code level before the wider feature
// (new pages/routes) was ready. The rename can't be undone at the DB level without a live
// migration, so this entity keeps the pre-revert Truck-facing API/route surface but maps onto
// the actual Vehicles table and satisfies its NOT NULL columns with sane defaults, none of which
// are exposed through TrucksController's DTOs yet.
public class Truck : BaseEntity
{
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = "Unspecified";
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public string? DriverId { get; set; }
    public string? Department { get; set; }
    public decimal Odometer { get; set; }
    public decimal? Capacity { get; set; }
    public TruckStatus Status { get; set; } = TruckStatus.Active;
    public string? AssetId { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public decimal? FuelBenchmarkKmPerLitre { get; set; }
    public string? VehicleClassId { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public decimal? LastServiceOdometer { get; set; }
    // Null means mileage-based tracking isn't configured for this truck yet — falls back to
    // NextServiceDate alone, so existing trucks don't suddenly show as "due" with no data behind it.
    public decimal? ServiceIntervalKm { get; set; }

    // Computed, never stored: no setter means EF Core's convention-based mapping skips it (not a
    // column), so it can never drift out of sync with Odometer/LastServiceOdometer/ServiceIntervalKm
    // the way a persisted "next due" value could if one of those changed without recalculating it.
    public decimal? NextServiceOdometer =>
        LastServiceOdometer.HasValue && ServiceIntervalKm.HasValue
            ? LastServiceOdometer + ServiceIntervalKm
            : null;

    public bool IsServiceDueByMileage => NextServiceOdometer.HasValue && Odometer >= NextServiceOdometer.Value;

    public virtual VehicleClass? VehicleClass { get; set; }
    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
}

public enum TruckStatus { Active, InMaintenance, Decommissioned, OutOfService }
