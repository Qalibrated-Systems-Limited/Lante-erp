using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Gifts;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IGiftHospitalityRepository : IGenericRepository<GiftHospitality>
{
    Task<PaginatedResult<GiftHospitality>> GetPagedAsync(GiftHospitalityFilterParameters parameters);
}
