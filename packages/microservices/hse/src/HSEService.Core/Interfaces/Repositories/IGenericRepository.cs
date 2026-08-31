using System.Linq.Expressions;
using HSEService.Core.DTOs.Common;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Repositories;

// The ONE repository interface for every HSE entity — plain CRUD plus a single predicate-based
// query escape hatch (FindAsync) so the handful of non-trivial lookups (RAMS versions for a site,
// training certs expiring soon, open corrective actions, ...) don't need a bespoke repository
// per entity. GenericRepository<T> is the only implementation; DI registers it as an open generic.
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id, bool includeDeleted = false);
    Task<IEnumerable<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
    Task<bool> RestoreAsync(string id);
}
