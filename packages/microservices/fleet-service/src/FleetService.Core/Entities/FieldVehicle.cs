namespace FleetService.Core.Entities;

/// <summary>Pool/field vehicle used by technicians on assignments — a separate registry from
/// <see cref="Vehicle"/> (the company's revenue-generating trucks). Ported from
/// operations-service's FieldVehicle so Fleet owns this data directly instead of reaching
/// into a different microservice.</summary>
public class FieldVehicle : BaseEntity
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public FieldVehicleType Type { get; set; } = FieldVehicleType.Other;
    public string? Color { get; set; }
    public decimal CurrentOdometer { get; set; }
    public FieldVehicleStatus Status { get; set; } = FieldVehicleStatus.Available;
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public decimal? LastServiceOdometer { get; set; }
    // Null means mileage-based tracking isn't configured for this vehicle yet — falls back to
    // NextServiceDate alone, so existing vehicles don't suddenly show as "due" with no data behind it.
    public decimal? ServiceIntervalKm { get; set; }
    // Legacy free-text field — unvalidated (ISO, dd/mm/yyyy, dd.mm.yyyy, or unparseable garbage
    // in production), which is exactly why nothing could ever check it for expiry: a string
    // can't be compared in SQL (#375). InsuranceExpiryDate below is the real, parsed value and
    // the one VehicleExpiryBackgroundService reads; this stays only for display/audit of
    // whatever the record originally had, and new writes should populate both.
    public string? InsuranceExpiry { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? Notes { get; set; }

    // Computed, never stored: no setter means EF Core's convention-based mapping skips it (not a
    // column), so it can never drift out of sync with the fields it derives from.
    public decimal? NextServiceOdometer =>
        LastServiceOdometer.HasValue && ServiceIntervalKm.HasValue
            ? LastServiceOdometer + ServiceIntervalKm
            : null;

    public bool IsServiceDueByMileage => NextServiceOdometer.HasValue && CurrentOdometer >= NextServiceOdometer.Value;

    public virtual ICollection<VehicleDispatch> Dispatches { get; set; } = new List<VehicleDispatch>();
    public virtual ICollection<FieldVehiclePhoto> Photos { get; set; } = new List<FieldVehiclePhoto>();
}

public enum FieldVehicleStatus { Available, Dispatched, UnderMaintenance, Decommissioned }
public enum FieldVehicleType { Probox, Pickup, Saloon, SUV, Van, Other }

/// <summary>Mirrors <see cref="MaterialPhoto"/> — Fleet owns its own photo storage rather than
/// reaching into operations-service's shared Attachments feature (which FieldVehicle photos
/// used before this, the same cross-module coupling FieldVehicle itself was ported out of).</summary>
public class FieldVehiclePhoto : BaseEntity
{
    public string FieldVehicleId { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }

    public virtual FieldVehicle? FieldVehicle { get; set; }
}
