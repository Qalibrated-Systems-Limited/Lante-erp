using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class VehicleDispatch : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public Assignment Assignment { get; set; } = null!;
    public string FieldVehicleId { get; set; } = string.Empty;
    public FieldVehicle FieldVehicle { get; set; } = null!;
    public string DriverName { get; set; } = string.Empty;
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

    public ICollection<FuelLog> FuelLogs { get; set; } = new List<FuelLog>();
}
