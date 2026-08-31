using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IFleetServiceClient
{
    Task<List<TruckDto>?> GetTrucksAsync();
    Task<List<TripResponseDto>?> GetTripsByTruckAsync(string truckId);
    Task<FleetPagedResult<TripResponseDto>?> GetTripsPageAsync(int pageNumber, int pageSize);
}
