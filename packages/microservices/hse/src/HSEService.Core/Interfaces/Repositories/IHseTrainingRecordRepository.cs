using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Training;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Repositories;

// Adds the employeeUserId filtered-and-paged listing that HseTrainingRecordsController's GetAll
// needs on top of the plain IGenericRepository<HseTrainingRecord> CRUD.
public interface IHseTrainingRecordRepository : IGenericRepository<HseTrainingRecord>
{
    Task<PaginatedResult<HseTrainingRecord>> GetPagedAsync(HseTrainingRecordFilterParameters parameters);
}
