using FleetService.Core.Entities;

namespace FleetService.Core.DTOs.FieldVehicle;

// ─── Field Vehicle registry ──────────────────────────────────────────────────

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
    public decimal? ServiceIntervalKm { get; set; }
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
    public decimal? ServiceIntervalKm { get; set; }
    public string? InsuranceExpiry { get; set; }
    public DateTime? InspectionExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class FieldVehicleResponseDto
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
    public decimal? ServiceIntervalKm { get; set; }
    public decimal? NextServiceOdometer { get; set; }
    public bool IsServiceDueByMileage { get; set; }
    public string? InsuranceExpiry { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
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

    /// <summary>Opt-in — excludes vehicles that already have a Pending or Dispatched dispatch
    /// record, even if their Status field still reads Available (Status only flips to Dispatched
    /// once a request is approved). Used by the Request Vehicle dropdown so it doesn't offer a
    /// vehicle that would just 400 on submit; the fleet registry's own status filter is untouched.</summary>
    public bool ExcludeOpenDispatch { get; set; }

    /// <summary>Null unless explicitly passed — dual-mode trigger. Omitted (as every caller
    /// today does except the list page's new paginated view) preserves the legacy unpaged
    /// flat-array response; passing it switches to a PagedResponseDto with a real TotalCount.</summary>
    public int? Page { get; set; }
    public int PageSize { get; set; } = 50;
}

// ─── Vehicle Dispatch ────────────────────────────────────────────────────────

public class CreateVehicleDispatchDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string FieldVehicleId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public DateTime DepartureDatetime { get; set; } = DateTime.UtcNow;
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

public class VehicleDispatchResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
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
    public List<DispatchFuelLogResponseDto> FuelLogs { get; set; } = [];
}

// ─── Dispatch Fuel Log ───────────────────────────────────────────────────────

public class CreateDispatchFuelLogDto
{
    public decimal AmountLitres { get; set; }
    public decimal CostKes { get; set; }
    public string? Location { get; set; }
    public DateTime? LoggedAt { get; set; }
    public string? Notes { get; set; }
}

public class DispatchFuelLogResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string DispatchId { get; set; } = string.Empty;
    public decimal AmountLitres { get; set; }
    public decimal CostKes { get; set; }
    public string? Location { get; set; }
    public DateTime LoggedAt { get; set; }
    public string? Notes { get; set; }
}
