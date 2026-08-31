using System.Linq.Expressions;
using ReportingService.Core.DTOs.Common;
using ReportingService.Core.Entities;

namespace ReportingService.Core.Interfaces.Repositories;

// The ONE repository interface for every Reporting-owned entity (schedules/recipients/runs, and
// later data sources/dashboards/scorecards/red-flags). Mirrors Compliance/Subcontracts/HSE.
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
