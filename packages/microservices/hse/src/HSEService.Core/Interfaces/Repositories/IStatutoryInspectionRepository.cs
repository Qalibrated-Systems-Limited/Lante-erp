using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Inspections;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Repositories;

// Adds the siteId filtered-and-paged listing that StatutoryInspectionsController's GetAll needs
// on top of the plain IGenericRepository<StatutoryInspection> CRUD.
public interface IStatutoryInspectionRepository : IGenericRepository<StatutoryInspection>
{
    Task<PaginatedResult<StatutoryInspection>> GetPagedAsync(StatutoryInspectionFilterParameters parameters);
}
