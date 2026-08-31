using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TicketWatcherRepository(TicketingDbContext context)
    : GenericRepository<TicketWatcher>(context), ITicketWatcherRepository
{
    public async Task<IEnumerable<TicketWatcher>> GetByTicketIdAsync(string ticketId)
    {
        return await Context.TicketWatchers
            .Where(w => w.TicketId == ticketId)
            .ToListAsync();
    }

    public async Task<TicketWatcher?> GetAsync(string ticketId, string userId)
    {
        return await Context.TicketWatchers
            .FirstOrDefaultAsync(w => w.TicketId == ticketId && w.UserId == userId);
    }
}
