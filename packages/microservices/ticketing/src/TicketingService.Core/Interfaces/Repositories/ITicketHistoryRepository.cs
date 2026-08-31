using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

// APPEND-ONLY — no UpdateAsync, no DeleteAsync
public interface ITicketHistoryRepository
{
    Task<IEnumerable<TicketHistory>> GetByTicketIdAsync(string ticketId);
    Task<TicketHistory> AppendAsync(TicketHistory entry);
}
