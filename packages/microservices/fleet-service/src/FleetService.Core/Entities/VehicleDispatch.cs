namespace FleetService.Core.Entities;

/// <summary>A technician's request/use of a <see cref="FieldVehicle"/> for a specific assignment.
/// Starts Pending — only a fleet-manager Approve marks the vehicle actually in use and applies
/// the odometer reading. AssignmentId is a raw string, no FK/nav: the Assignment itself lives in
/// operations-service, a different microservice with its own database.</summary>
public class VehicleDispatch : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string FieldVehicleId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;

    /// <summary>The user (ClaimTypes.NameIdentifier) who submitted this request, used to notify
    /// them on approval. Nullable — dispatches created before this field existed have no value,
    /// and approval notification is skipped gracefully for those.</summary>
    public string? RequestedByUserId { get; set; }
    public DateTime DepartureDatetime { get; set; }
    public decimal DepartureOdometer { get; set; }
    public string FuelLevelOut { get; set; } = string.Empty;
    public DateTime? ReturnDatetime { get; set; }
    public decimal? ReturnOdometer { get; set; }
    public string? FuelLevelIn { get; set; }
    public string? Notes { get; set; }
    public VehicleDispatchStatus Status { get; set; } = VehicleDispatchStatus.Pending;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public virtual FieldVehicle? FieldVehicle { get; set; }
    public virtual ICollection<DispatchFuelLog> FuelLogs { get; set; } = new List<DispatchFuelLog>();
}

public enum VehicleDispatchStatus { Pending, Dispatched, Returned, Rejected, Cancelled }
