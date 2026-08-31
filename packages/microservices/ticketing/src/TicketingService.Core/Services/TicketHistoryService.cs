using AutoMapper;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class TicketHistoryService(ITicketHistoryRepository historyRepository, IMapper mapper) : ITicketHistoryService
{
    public async Task AppendAsync(string ticketId, string userId, string action, string? fromValue = null, string? toValue = null, string? notes = null)
    {
        var entry = new TicketHistory
        {
            TicketId = ticketId,
            UserId = userId,
            Action = action,
            FromValue = fromValue,
            ToValue = toValue,
            Notes = notes,
            OccurredAt = DateTime.UtcNow
        };
        await historyRepository.AppendAsync(entry);
    }

    public async Task<IEnumerable<TicketHistoryReadDto>> GetByTicketIdAsync(string ticketId)
    {
        var history = await historyRepository.GetByTicketIdAsync(ticketId);
        return mapper.Map<IEnumerable<TicketHistoryReadDto>>(history);
    }
}
