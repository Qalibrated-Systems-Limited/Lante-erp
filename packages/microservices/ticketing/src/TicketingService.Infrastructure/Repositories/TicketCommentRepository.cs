using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TicketCommentRepository(TicketingDbContext context)
    : GenericRepository<TicketComment>(context), ITicketCommentRepository
{
    public async Task<IEnumerable<TicketComment>> GetByTicketIdAsync(string ticketId)
    {
        return await Context.TicketComments
            .Include(c => c.Attachments)
            .Where(c => c.TicketId == ticketId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
