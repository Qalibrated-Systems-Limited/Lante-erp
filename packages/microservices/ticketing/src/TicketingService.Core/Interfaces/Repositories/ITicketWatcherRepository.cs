using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ITicketWatcherRepository : IGenericRepository<TicketWatcher>
{
    Task<IEnumerable<TicketWatcher>> GetByTicketIdAsync(string ticketId);
    Task<TicketWatcher?> GetAsync(string ticketId, string userId);
}
