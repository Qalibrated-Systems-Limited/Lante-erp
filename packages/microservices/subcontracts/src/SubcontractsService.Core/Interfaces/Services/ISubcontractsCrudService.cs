using System.Linq.Expressions;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.Entities;

namespace SubcontractsService.Core.Interfaces.Services;

// The ONE CRUD service for every Subcontracts entity — controllers depend on
// ISubcontractsCrudService<T> directly for plain list/get/create/update/delete, so no database
// access ever happens in a controller. Entities whose create/update is a genuine multi-entity
// business process (PQQ approval syncing the ASR, scorecard syncing the watch list, mobilization's
// RAMS gate) get a small dedicated workflow service built on top of this one, never on the
// repository directly.
public interface ISubcontractsCrudService<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
}
