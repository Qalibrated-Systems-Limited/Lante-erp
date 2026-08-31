using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class EscalationRuleRepository(TicketingDbContext context)
    : GenericRepository<EscalationRule>(context), IEscalationRuleRepository
{
    public async Task<IEnumerable<EscalationRule>> GetByCategoryAndPriorityAsync(string categoryId, TicketPriority priority)
    {
        return await Context.EscalationRules
            .Where(r => r.CategoryId == categoryId && r.Priority == priority)
            .OrderBy(r => r.TriggerAfterHours)
            .ToListAsync();
    }
}
