using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface ITripRepository : IRepository<Trip>
{
    Task<IEnumerable<Trip>> GetByDriverIdAsync(string driverId);
    Task<IEnumerable<Trip>> GetByTruckIdAsync(string truckId);
    Task<(IEnumerable<Trip> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? driverId = null, string? status = null);
    Task<Trip?> GetByIdWithDetailsAsync(string id);
    Task<bool> TripTypeExistsAsync(string tripTypeId);
    Task<IEnumerable<object>> GetTripTypesAsync();
    Task<bool> MaterialExistsAsync(string materialId);
    Task<bool> MaterialVariantExistsAsync(string variantId);
}
