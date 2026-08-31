using Microsoft.EntityFrameworkCore;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Repositories;

public class MaterialRepository(FleetServiceDbContext context)
    : Repository<Material>(context), IMaterialRepository
{
    public async Task<IEnumerable<Material>> GetAllWithDetailsAsync()
        => await _context.Materials
            .Include(m => m.Variants).ThenInclude(v => v.Photos)
            .Include(m => m.Photos)
            .OrderBy(m => m.Name)
            .ToListAsync();

    public Task<Material?> GetByIdWithDetailsAsync(string id)
        => _context.Materials
            .Include(m => m.Variants).ThenInclude(v => v.Photos)
            .Include(m => m.Photos)
            .Include(m => m.Costs)
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<(IEnumerable<Material> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        var query = _context.Materials.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(s) || (m.Description != null && m.Description.ToLower().Contains(s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
