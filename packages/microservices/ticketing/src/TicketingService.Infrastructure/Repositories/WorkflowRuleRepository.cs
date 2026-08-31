using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class WorkflowRuleRepository(TicketingDbContext context)
    : GenericRepository<WorkflowRule>(context), IWorkflowRuleRepository
{
    public async Task<IEnumerable<WorkflowRule>> GetActiveByTriggerEventAsync(WorkflowTriggerEvent triggerEvent) =>
        await DbSet
            .Where(r => r.IsActive && r.TriggerEvent == triggerEvent)
            .OrderBy(r => r.RunOrder)
            .ToListAsync();
}
