using System.ComponentModel.DataAnnotations;
using FleetService.Core.Entities;

namespace FleetService.Core.DTOs.Trip;

public class CreateTripDto
{
    public string TruckId { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string TripTypeId { get; set; } = string.Empty;
    public string? CustomTripType { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string EndLocation { get; set; } = string.Empty;
    public double? StartLocationLatitude { get; set; }
    public double? StartLocationLongitude { get; set; }
    public double? EndLocationLatitude { get; set; }
    public double? EndLocationLongitude { get; set; }
    [Required(ErrorMessage = "StartMileage is required")]
    [Range(0, double.MaxValue, ErrorMessage = "StartMileage must be 0 or greater")]
    public decimal? StartMileage { get; set; }
    public string? MaterialId { get; set; }
    public string? MaterialVariantId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? LinkedTicketId { get; set; }
    [Required(ErrorMessage = "Revenue is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Revenue must be 0 or greater")]
    public decimal Revenue { get; set; }
}

public class StartTripDto { }

public class UpdateTripDto
{
    public string? CustomTripType { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string EndLocation { get; set; } = string.Empty;
    public double? CurrentLocationLatitude { get; set; }
    public double? CurrentLocationLongitude { get; set; }
    public decimal? EndMileage { get; set; }
    public string? MaterialId { get; set; }
    public string? MaterialVariantId { get; set; }
    public decimal? MaterialCost { get; set; }
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
    public string? MaterialId { get; set; }
    public string? MaterialName { get; set; }
    public string? MaterialPhotoUrl { get; set; }
    public string? OdometerStartPhotoUrl { get; set; }
    public string? OdometerEndPhotoUrl { get; set; }
    public List<string> TripPhotos { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
