using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface ITaxComplianceCertRepository : IGenericRepository<TaxComplianceCert>
{
    new Task<PaginatedResult<TaxComplianceCert>> GetPagedAsync(PaginationParameters parameters);
}
