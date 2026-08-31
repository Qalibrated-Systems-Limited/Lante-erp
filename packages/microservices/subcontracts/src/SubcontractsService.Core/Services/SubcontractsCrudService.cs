using System.Linq.Expressions;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Interfaces.Repositories;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Core.Services;

public class SubcontractsCrudService<T>(IGenericRepository<T> repository) : ISubcontractsCrudService<T>
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
