using System.Linq.Expressions;
using HrService.Core.Entities;

namespace HrService.Core.Interfaces.Repositories;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    /// <summary>Soft delete: sets <see cref="BaseEntity.IsDeleted"/> rather than removing the row. See #332.</summary>
    Task DeleteAsync(T entity);
    Task RestoreAsync(T entity);
    Task<bool> ExistsAsync(string id);
    IQueryable<T> Query();
}
