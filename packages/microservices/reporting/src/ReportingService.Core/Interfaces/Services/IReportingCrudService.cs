using System.Linq.Expressions;
using ReportingService.Core.DTOs.Common;
using ReportingService.Core.Entities;

namespace ReportingService.Core.Interfaces.Services;

// The ONE CRUD service for every Reporting-owned entity — controllers depend on
// IReportingCrudService<T> directly, so no database access happens in a controller.
public interface IReportingCrudService<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
}
