using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ISLAPolicyRepository : IGenericRepository<SLAPolicy>
{
    Task<SLAPolicy?> GetByCategoryAndPriorityAsync(string categoryId, TicketPriority priority);
}
