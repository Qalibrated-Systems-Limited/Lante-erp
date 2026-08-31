using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Services;

public interface IFleetServiceClient
{
    /// <summary>
    /// Creates a Trip in FleetService derived from the ticket context.
    /// Returns the created trip ID, or null on failure.
    /// </summary>
    Task<string?> CreateTripFromTicketAsync(Ticket ticket, string requestedByUserId, string? bearerToken);
}
