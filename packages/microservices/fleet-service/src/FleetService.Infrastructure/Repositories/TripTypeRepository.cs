using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class TripTypeRepository(FleetServiceDbContext context)
    : Repository<TripType>(context), ITripTypeRepository
{
    public async Task<(IEnumerable<TripType> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, bool? isActive = null)
    {
        var query = _context.TripTypes.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
