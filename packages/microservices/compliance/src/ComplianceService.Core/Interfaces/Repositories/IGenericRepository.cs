using System.Linq.Expressions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

// The ONE repository interface for every Compliance entity — plain CRUD plus a
// predicate-based query escape hatch (FindAsync). GenericRepository<T> is the only
// implementation; DI registers it as an open generic. Mirrors HSE's convention.
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
