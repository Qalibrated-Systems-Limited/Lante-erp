using UserService.Core.DTOs.Common;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id, bool includeDeleted = false);
    Task<IEnumerable<T>> GetAllAsync();
    Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
    Task<bool> RestoreAsync(string id);
}
