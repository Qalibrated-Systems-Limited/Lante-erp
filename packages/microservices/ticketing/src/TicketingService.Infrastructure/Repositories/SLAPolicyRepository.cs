using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class SLAPolicyRepository(TicketingDbContext context)
    : GenericRepository<SLAPolicy>(context), ISLAPolicyRepository
{
    public async Task<SLAPolicy?> GetByCategoryAndPriorityAsync(string categoryId, TicketPriority priority)
    {
        return await Context.SLAPolicies
            .FirstOrDefaultAsync(s => s.CategoryId == categoryId && s.Priority == priority);
    }
}
