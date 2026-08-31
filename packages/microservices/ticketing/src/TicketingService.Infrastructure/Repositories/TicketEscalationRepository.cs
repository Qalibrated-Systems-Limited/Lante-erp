using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TicketEscalationRepository(TicketingDbContext context)
    : GenericRepository<TicketEscalation>(context), ITicketEscalationRepository
{
    public async Task<IEnumerable<TicketEscalation>> GetActiveEscalationsAsync()
    {
        return await Context.TicketEscalations
            .Where(e => !e.IsAcknowledged)
            .OrderByDescending(e => e.EscalatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketEscalation>> GetByTicketIdAsync(string ticketId)
    {
        return await Context.TicketEscalations
            .Where(e => e.TicketId == ticketId)
            .OrderByDescending(e => e.EscalatedAt)
            .ToListAsync();
    }
}
