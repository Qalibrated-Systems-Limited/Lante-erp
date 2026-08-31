using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Services;

public interface ISLAService
{
    Task<SLADeadlines> CalculateSLADeadlinesAsync(string categoryId, TicketPriority priority, DateTime createdAt);
    Task<IEnumerable<Ticket>> CheckSLABreachesAsync();
    /// D2-2 — tickets that have consumed ≥ their amber threshold (default 75%) of the resolution SLA
    /// window but haven't breached yet (the third "approaching breach" state).
    Task<IEnumerable<Ticket>> GetAmberTicketsAsync();
    /// D2-4 — tickets left unassigned longer than <paramref name="minutes"/> of active (unpaused) time.
    Task<IEnumerable<Ticket>> GetUnassignedTicketsAsync(int minutes);
    Task<SLASummaryDto> GetSLASummaryAsync(string? departmentId = null);
    bool IsResponseBreached(Ticket ticket);
    bool IsResolutionBreached(Ticket ticket);
    Task<IEnumerable<SLAPolicyReadDto>> GetPoliciesByCategoryAsync(string categoryId);
    Task<SLAPolicyReadDto> AddPolicyAsync(CreateSLAPolicyDto dto, string createdByUserId);
}
