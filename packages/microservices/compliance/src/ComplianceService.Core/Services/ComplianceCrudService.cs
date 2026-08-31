using System.Linq.Expressions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Core.Services;

public class ComplianceCrudService<T>(IGenericRepository<T> repository) : IComplianceCrudService<T>
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
