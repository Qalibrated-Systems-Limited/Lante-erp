using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Repositories;

public interface IWorkflowRuleRepository : IGenericRepository<WorkflowRule>
{
    Task<IEnumerable<WorkflowRule>> GetActiveByTriggerEventAsync(WorkflowTriggerEvent triggerEvent);
}
