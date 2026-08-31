using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class TripRepository(FleetServiceDbContext context)
    : Repository<Trip>(context), ITripRepository
{
    public async Task<IEnumerable<Trip>> GetByDriverIdAsync(string driverId)
        => await _context.Trips
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Trip>> GetByTruckIdAsync(string truckId)
        => await _context.Trips
            .Where(t => t.TruckId == truckId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<(IEnumerable<Trip> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? driverId = null, string? status = null)
    {
        var query = _context.Trips.AsQueryable();

        if (!string.IsNullOrEmpty(driverId))
            query = query.Where(t => t.DriverId == driverId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TripStatus>(status, true, out var statusEnum))
            query = query.Where(t => t.Status == statusEnum);

        var total = await query.CountAsync();
        var items = await query
            .Include(t => t.TripType)
            .Include(t => t.Truck)
            .Include(t => t.Material)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Trip?> GetByIdWithDetailsAsync(string id)
        => await _context.Trips
            .Include(t => t.Truck)
            .Include(t => t.TripType)
            .Include(t => t.Material)
            .Include(t => t.MaterialVariant)
            .Include(t => t.Expenses)
            .FirstOrDefaultAsync(t => t.Id == id);

    public Task<bool> TripTypeExistsAsync(string tripTypeId)
        => _context.TripTypes.AnyAsync(t => t.Id == tripTypeId);

    public Task<bool> MaterialExistsAsync(string materialId)
        => _context.Materials.AnyAsync(m => m.Id == materialId);

    public Task<bool> MaterialVariantExistsAsync(string variantId)
        => _context.MaterialVariants.AnyAsync(v => v.Id == variantId);

    public async Task<IEnumerable<object>> GetTripTypesAsync()
        => await _context.TripTypes
            .Select(t => new { t.Id, t.Name, t.Category } as object)
            .ToListAsync();
}
