using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Incidents;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Repositories;

// Adds the incidentId/openOnly filtered-and-paged listing that CorrectiveActionsController's
// GetAll needs on top of the plain IGenericRepository<CorrectiveAction> CRUD.
public interface ICorrectiveActionRepository : IGenericRepository<CorrectiveAction>
{
    Task<PaginatedResult<CorrectiveAction>> GetPagedAsync(CorrectiveActionFilterParameters parameters);
}
