using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.DTOs.Tickets;

namespace TicketingService.Core.Interfaces.Services;

public interface IEscalationService
{
    Task EvaluateEscalationsAsync(string tenantId);
    Task<IEnumerable<TicketEscalationDto>> GetByTicketIdAsync(string ticketId);
    Task<bool> AcknowledgeAsync(string ticketId, string escalationId, string acknowledgedByUserId);
    Task<IEnumerable<EscalationRuleReadDto>> GetRulesByCategoryAsync(string categoryId);
    Task<EscalationRuleReadDto> AddRuleAsync(CreateEscalationRuleDto dto, string createdByUserId);
}
