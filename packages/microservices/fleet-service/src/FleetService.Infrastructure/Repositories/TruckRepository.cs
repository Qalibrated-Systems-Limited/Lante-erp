using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class TruckRepository(FleetServiceDbContext context)
    : Repository<Truck>(context), ITruckRepository
{
    public async Task<(IEnumerable<Truck> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? driverId = null, string? search = null)
    {
        var query = _context.Trucks.AsQueryable();

        if (!string.IsNullOrEmpty(driverId))
            query = query.Where(t => t.DriverId == driverId);

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = query.Where(t => t.LicensePlate.ToLower().Contains(s) || t.Model.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.LicensePlate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> TryAdvanceOdometerAsync(string truckId, decimal newOdometer)
    {
        var rows = await _context.Trucks
            .Where(t => t.Id == truckId && t.Odometer < newOdometer)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Odometer, newOdometer)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));
        return rows > 0;
    }
}
