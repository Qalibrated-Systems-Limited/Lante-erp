using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITicketCommentRepository : IGenericRepository<TicketComment>
{
    Task<IEnumerable<TicketComment>> GetByTicketIdAsync(string ticketId);
}
