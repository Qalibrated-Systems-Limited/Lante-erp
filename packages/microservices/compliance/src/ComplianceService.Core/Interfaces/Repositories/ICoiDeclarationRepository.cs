using ComplianceService.Core.DTOs.Coi;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface ICoiDeclarationRepository : IGenericRepository<CoiDeclaration>
{
    Task<PaginatedResult<CoiDeclaration>> GetPagedAsync(CoiDeclarationFilterParameters parameters);
}
