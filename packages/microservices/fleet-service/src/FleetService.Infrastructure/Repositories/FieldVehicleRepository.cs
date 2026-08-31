using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class FieldVehicleRepository(FleetServiceDbContext context)
    : Repository<FieldVehicle>(context), IFieldVehicleRepository
{
    public async Task<(IEnumerable<FieldVehicle> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null, FieldVehicleStatus? status = null, FieldVehicleType? type = null)
    {
        var query = _context.FieldVehicles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(v =>
                v.RegistrationNumber.ToLower().Contains(s) ||
                v.Make.ToLower().Contains(s) ||
                v.Model.ToLower().Contains(s));
        }
        if (status.HasValue) query = query.Where(v => v.Status == status.Value);
        if (type.HasValue) query = query.Where(v => v.Type == type.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(v => v.RegistrationNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
