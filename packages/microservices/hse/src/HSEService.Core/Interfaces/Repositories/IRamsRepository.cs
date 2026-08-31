using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Rams;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Repositories;

// Adds the siteId filtered-and-paged listing that RamsController's GetAll needs on top of the
// plain IGenericRepository<Rams> CRUD.
public interface IRamsRepository : IGenericRepository<Rams>
{
    Task<PaginatedResult<Rams>> GetPagedAsync(RamsFilterParameters parameters);
}
