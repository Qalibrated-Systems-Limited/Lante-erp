using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IStatutoryObligationRepository : IGenericRepository<StatutoryObligation>
{
    // GetAll only ever surfaces active obligations — this is a fixed business rule, not a
    // user-supplied filter, so it doesn't need its own FilterParameters type.
    new Task<PaginatedResult<StatutoryObligation>> GetPagedAsync(PaginationParameters parameters);
}
