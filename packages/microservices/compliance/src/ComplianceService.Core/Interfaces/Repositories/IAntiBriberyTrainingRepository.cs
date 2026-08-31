using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Training;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IAntiBriberyTrainingRepository : IGenericRepository<AntiBriberyTraining>
{
    Task<PaginatedResult<AntiBriberyTraining>> GetPagedAsync(AntiBriberyTrainingFilterParameters parameters);
}
