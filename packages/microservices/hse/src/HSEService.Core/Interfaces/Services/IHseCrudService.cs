using System.Linq.Expressions;
using HSEService.Core.DTOs.Common;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Services;

// The ONE CRUD service for every HSE entity — controllers depend on IHseCrudService&lt;T&gt;
// directly for plain list/get/create/update/delete, so no database access ever happens in a
// controller. Entities whose create/update is a genuine multi-entity business process (incident
// + corrective action + env incident, RAMS versioning, toolbox talk + attendees, KPI aggregation)
// get a small dedicated workflow service that itself is built on top of this one, never on the
// repository directly.
public interface IHseCrudService<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
}
