using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

// APPEND-ONLY — no update, no delete
public class TicketHistoryRepository(TicketingDbContext context) : ITicketHistoryRepository
{
    private readonly TicketingDbContext _context = context;

    public async Task<IEnumerable<TicketHistory>> GetByTicketIdAsync(string ticketId)
    {
        return await _context.TicketHistories
            .Where(h => h.TicketId == ticketId)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
    }

    public async Task<TicketHistory> AppendAsync(TicketHistory entry)
    {
        await _context.TicketHistories.AddAsync(entry);
        await _context.SaveChangesAsync();
        return entry;
    }
}
