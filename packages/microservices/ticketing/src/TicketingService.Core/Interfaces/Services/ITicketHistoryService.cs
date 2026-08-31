using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Services;

public interface ITicketHistoryService
{
    Task AppendAsync(string ticketId, string userId, string action, string? fromValue = null, string? toValue = null, string? notes = null);
    Task<IEnumerable<TicketHistoryReadDto>> GetByTicketIdAsync(string ticketId);
}
