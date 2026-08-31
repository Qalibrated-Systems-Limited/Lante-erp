using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITicketAttachmentRepository : IGenericRepository<TicketAttachment>
{
    Task<IEnumerable<TicketAttachment>> GetByTicketIdAsync(string ticketId);
    Task<int> CountByTicketIdAsync(string ticketId);
}
