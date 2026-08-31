using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class TicketAttachmentRepository(TicketingDbContext context)
    : GenericRepository<TicketAttachment>(context), ITicketAttachmentRepository
{
    public async Task<IEnumerable<TicketAttachment>> GetByTicketIdAsync(string ticketId)
        => await DbSet.Where(a => a.TicketId == ticketId).OrderBy(a => a.CreatedAt).ToListAsync();

    public async Task<int> CountByTicketIdAsync(string ticketId)
        => await DbSet.CountAsync(a => a.TicketId == ticketId);
}
