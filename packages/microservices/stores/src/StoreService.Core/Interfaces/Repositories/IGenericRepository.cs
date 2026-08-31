using System.Linq.Expressions;
using StoreService.Core.Entities;

namespace StoreService.Core.Interfaces.Repositories;

/// <summary>The one repository abstraction reused across every Stores entity — no per-entity
/// repository classes. Registered once as an open generic in InfrastructureServiceRegistration.</summary>
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsAsync(string id);
    IQueryable<T> Query();
}
