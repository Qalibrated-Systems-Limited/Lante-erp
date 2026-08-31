using System.Linq.Expressions;
using HSEService.Core.DTOs.Common;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Core.Services;

public class HseCrudService<T>(IGenericRepository<T> repository) : IHseCrudService<T>
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
