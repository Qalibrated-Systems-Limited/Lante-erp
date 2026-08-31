using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class ComplaintWorkflowStepRepository(TicketingDbContext context)
    : GenericRepository<ComplaintWorkflowStep>(context), IComplaintWorkflowStepRepository
{
    public async Task<IEnumerable<ComplaintWorkflowStep>> GetByTicketAsync(string ticketId)
    {
        return await Context.ComplaintWorkflowSteps
            .Where(s => s.TicketId == ticketId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();
    }
}
