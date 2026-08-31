using System.Linq.Expressions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Services;

// The ONE CRUD service for every Compliance entity — controllers depend on
// IComplianceCrudService&lt;T&gt; directly, so no database access ever happens in a
// controller. There are no multi-entity workflows in this module beyond the
// dashboard aggregator, so this alone backs every controller's persistence.
public interface IComplianceCrudService<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
}
