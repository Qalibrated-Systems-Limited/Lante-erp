using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

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
    public string? InsuranceExpiry { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? Notes { get; set; }

    public ICollection<VehicleDispatch> Dispatches { get; set; } = new List<VehicleDispatch>();
}
