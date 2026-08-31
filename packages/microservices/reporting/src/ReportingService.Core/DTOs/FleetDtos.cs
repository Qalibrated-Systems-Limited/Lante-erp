namespace ReportingService.Core.DTOs;

// Mirrors FleetService.Core entity/DTO shapes returned by GET /api/v1/Trucks and /api/v1/Trips.
// TrucksController returns the raw Truck entity (not a projected DTO), so the fields below match
// FleetService.Core.Entities.Truck exactly (camelCased on the wire).

public class TruckDto
{
    public string Id { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? DriverId { get; set; }
}

public class TripResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string TruckId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string TripTypeId { get; set; } = string.Empty;
    public string? TripTypeName { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string EndLocation { get; set; } = string.Empty;
    public decimal? StartMileage { get; set; }
    public decimal? EndMileage { get; set; }
    public decimal? TotalMileage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
    public string? LinkedTicketId { get; set; }
    public DateTime CreatedAt { get; set; }
}
