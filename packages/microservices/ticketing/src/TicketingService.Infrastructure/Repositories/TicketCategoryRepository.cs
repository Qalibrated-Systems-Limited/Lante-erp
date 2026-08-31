using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TicketCategoryRepository(TicketingDbContext context)
    : GenericRepository<TicketCategory>(context), ITicketCategoryRepository
{
    public async Task<IEnumerable<TicketCategory>> GetByDepartmentAsync(string departmentId)
    {
        return await Context.TicketCategories
            .Where(c => c.DepartmentId == departmentId && c.IsActive)
            .ToListAsync();
    }

    public async Task<TicketCategory?> GetByNameAsync(string name)
    {
        return await Context.TicketCategories
            .FirstOrDefaultAsync(c => c.Name == name);
    }
}
