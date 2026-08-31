using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Incidents;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Services;

// The multi-entity process behind HSE-001/007: reporting an incident can also spawn a
// CorrectiveAction and (for environmental events) an EnvIncident 1:1 extension. Built entirely on
// top of IHseCrudService&lt;T&gt; — never touches a repository directly.
public interface IHseIncidentWorkflowService
{
    Task<HseIncident> CreateAsync(CreateHseIncidentDto dto, string reportedByUserId, string? reportedByName, string? tenantSchema);
    Task<HseIncident?> GetWithDetailsAsync(string id);
    Task<List<HseIncident>> GetAllWithDetailsAsync();
    Task<PaginatedResult<HseIncident>> GetPagedWithDetailsAsync(PaginationParameters parameters);
    Task<CorrectiveAction> AddCorrectiveActionAsync(CreateCorrectiveActionDto dto, string? tenantSchema);
}
