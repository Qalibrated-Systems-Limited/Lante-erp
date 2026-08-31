using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Services;

public interface ITicketAssignmentClient
{
    /// <summary>
    /// Creates an Assignment in the Operations service derived from the ticket context.
    /// Returns the created assignment ID, or null on failure.
    /// </summary>
    Task<string?> CreateAssignmentFromTicketAsync(Ticket ticket, string managerUserId, string? bearerToken);
}
