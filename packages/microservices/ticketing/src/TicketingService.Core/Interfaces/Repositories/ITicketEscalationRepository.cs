using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITicketEscalationRepository : IGenericRepository<TicketEscalation>
{
    Task<IEnumerable<TicketEscalation>> GetActiveEscalationsAsync();
    Task<IEnumerable<TicketEscalation>> GetByTicketIdAsync(string ticketId);
}
