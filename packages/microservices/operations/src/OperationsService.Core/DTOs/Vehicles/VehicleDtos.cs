using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Vehicles;

// ─── Field Vehicle ───────────────────────────────────────────────────────────

public class CreateFieldVehicleDto
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public FieldVehicleType Type { get; set; } = FieldVehicleType.Other;
    public string? Color { get; set; }
    public decimal CurrentOdometer { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public decimal? LastServiceOdometer { get; set; }
    public string? InsuranceExpiry { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateFieldVehicleDto
{
    public string? RegistrationNumber { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public FieldVehicleType? Type { get; set; }
    public string? Color { get; set; }
    public decimal? CurrentOdometer { get; set; }
    public FieldVehicleStatus? Status { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public decimal? LastServiceOdometer { get; set; }
    public string? InsuranceExpiry { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class FieldVehicleReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Color { get; set; }
    public decimal CurrentOdometer { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public decimal? LastServiceOdometer { get; set; }
    public string? InsuranceExpiry { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FieldVehicleFilterParameters
{
    public string? Search { get; set; }
    public FieldVehicleStatus? Status { get; set; }
    public FieldVehicleType? Type { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// ─── Vehicle Dispatch ────────────────────────────────────────────────────────

public class CreateVehicleDispatchDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string FieldVehicleId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public DateTime DepartureDatetime { get; set; } = DateTime.UtcNow;
    public decimal DepartureOdometer { get; set; }
    public string FuelLevelOut { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ApproveDispatchDto
{
    public string ApprovedBy { get; set; } = string.Empty;
}

public class LogReturnDto
{
    public DateTime ReturnDatetime { get; set; } = DateTime.UtcNow;
    public decimal ReturnOdometer { get; set; }
    public string FuelLevelIn { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class VehicleDispatchReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string AssignmentTitle { get; set; } = string.Empty;
    public string FieldVehicleId { get; set; } = string.Empty;
    public string VehicleRegistration { get; set; } = string.Empty;
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public DateTime DepartureDatetime { get; set; }
    public decimal DepartureOdometer { get; set; }
    public string FuelLevelOut { get; set; } = string.Empty;
    public DateTime? ReturnDatetime { get; set; }
    public decimal? ReturnOdometer { get; set; }
    public string? FuelLevelIn { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FuelLogReadDto> FuelLogs { get; set; } = [];
}

// ─── Fuel Log ────────────────────────────────────────────────────────────────

public class CreateFuelLogDto
{
    public string DispatchId { get; set; } = string.Empty;
    public decimal AmountLitres { get; set; }
    public decimal CostKes { get; set; }
    public string? Location { get; set; }
    public DateTime? LoggedAt { get; set; }
    public string? Notes { get; set; }
}

public class FuelLogReadDto
{
    public string Id { get; set; } = string.Empty;
    public string DispatchId { get; set; } = string.Empty;
    public decimal AmountLitres { get; set; }
    public decimal CostKes { get; set; }
    public string? Location { get; set; }
    public DateTime LoggedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
