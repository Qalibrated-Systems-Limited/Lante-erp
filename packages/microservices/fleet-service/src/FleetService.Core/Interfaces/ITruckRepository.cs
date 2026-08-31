using FleetService.Core.Entities;

namespace FleetService.Core.Interfaces;

public interface ITruckRepository : IRepository<Truck>
{
    Task<(IEnumerable<Truck> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? driverId = null, string? search = null);

    /// <summary>Atomically sets Odometer to <paramref name="newOdometer"/> only if it's currently
    /// lower — a plain read-then-write here lets two concurrent trip completions race: both read
    /// the same starting value, both pass an in-memory "is this higher?" check, and whichever
    /// SaveChanges commits last wins, silently regressing the odometer. Returns false if the
    /// truck doesn't exist or its odometer was already at/above <paramref name="newOdometer"/>.</summary>
    Task<bool> TryAdvanceOdometerAsync(string truckId, decimal newOdometer);
}
