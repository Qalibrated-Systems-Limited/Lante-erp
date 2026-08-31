using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Repositories;

public interface IEscalationRuleRepository : IGenericRepository<EscalationRule>
{
    Task<IEnumerable<EscalationRule>> GetByCategoryAndPriorityAsync(string categoryId, TicketPriority priority);
}
