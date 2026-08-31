namespace FleetService.Core.Entities;

public class Trip : BaseEntity
{
    public string TruckId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    // Nullable (rather than required) so a trip whose type was later deactivated/soft-deleted
    // doesn't silently disappear from list queries — EF Core's Include() optimizes a *required*
    // reference navigation's LEFT JOIN into an INNER JOIN, and once the referenced TripType row
    // fails its own global soft-delete filter, that INNER JOIN drops the Trip row entirely (the
    // Count query, run before Include is added, still counts it — hence "N total trips, 0 shown").
    public string? TripTypeId { get; set; }
    public string? CustomTripType { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string StartLocation { get; set; } = string.Empty;
    public string EndLocation { get; set; } = string.Empty;
    public double? StartLocationLatitude { get; set; }
    public double? StartLocationLongitude { get; set; }
    public double? EndLocationLatitude { get; set; }
    public double? EndLocationLongitude { get; set; }
    public double? TruckLocationLatitude { get; set; }
    public double? TruckLocationLongitude { get; set; }
    public double? CurrentLocationLatitude { get; set; }
    public double? CurrentLocationLongitude { get; set; }
    public decimal? StartMileage { get; set; }
    public decimal? EndMileage { get; set; }
    public decimal? TotalMileage { get; set; }
    public string? MaterialPhotoUrl { get; set; }
    public string? OdometerStartPhotoUrl { get; set; }
    public string? OdometerEndPhotoUrl { get; set; }
    public string? TripPhotosJson { get; set; }
    public string? MaterialId { get; set; }
    public string? MaterialVariantId { get; set; }
    public decimal? MaterialCost { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Pending;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? LinkedTicketId { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }

    public virtual Truck? Truck { get; set; }
    public virtual TripType? TripType { get; set; }
    public virtual Material? Material { get; set; }
    public virtual MaterialVariant? MaterialVariant { get; set; }
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public virtual ICollection<TripDeposit> Deposits { get; set; } = new List<TripDeposit>();
}

public enum TripStatus { Pending, InProgress, Completed, Cancelled }
