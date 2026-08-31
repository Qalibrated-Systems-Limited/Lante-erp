using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Services;

public interface IWorkflowEngine
{
    Task TriggerAsync(WorkflowTriggerEvent triggerEvent, Ticket ticket, string triggeredByUserId);
}
