using System.Linq.Expressions;
using ReportingService.Core.DTOs.Common;
using ReportingService.Core.Entities;
using ReportingService.Core.Interfaces.Repositories;
using ReportingService.Core.Interfaces.Services;

namespace ReportingService.Core.Services;

public class ReportingCrudService<T>(IGenericRepository<T> repository) : IReportingCrudService<T>
    where T : BaseEntity
{
    public Task<T?> GetByIdAsync(string id) => repository.GetByIdAsync(id);
    public Task<IEnumerable<T>> GetAllAsync() => repository.GetAllAsync();
    public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) => repository.FindAsync(predicate);
    public Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters) => repository.GetPagedAsync(parameters);
    public Task<T> CreateAsync(T entity) => repository.CreateAsync(entity);
    public Task<T> UpdateAsync(T entity) => repository.UpdateAsync(entity);
    public Task<bool> DeleteAsync(string id) => repository.DeleteAsync(id);
}
