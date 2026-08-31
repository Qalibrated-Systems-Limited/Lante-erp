using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Ppe;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Repositories;

// Adds the employeeUserId filtered-and-paged listing that PpeIssuesController's GetAll needs on
// top of the plain IGenericRepository<PpeIssue> CRUD.
public interface IPpeIssueRepository : IGenericRepository<PpeIssue>
{
    Task<PaginatedResult<PpeIssue>> GetPagedAsync(PpeIssueFilterParameters parameters);
}
