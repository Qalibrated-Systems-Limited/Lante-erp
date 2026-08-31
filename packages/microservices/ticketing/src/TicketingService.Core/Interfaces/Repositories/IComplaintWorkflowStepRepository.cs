using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface IComplaintWorkflowStepRepository : IGenericRepository<ComplaintWorkflowStep>
{
    /// All steps for a ticket, ordered by StepNumber (1..5).
    Task<IEnumerable<ComplaintWorkflowStep>> GetByTicketAsync(string ticketId);
}
