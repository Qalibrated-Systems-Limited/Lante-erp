using System.Linq.Expressions;
using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IService<T> where T : BaseEntity
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(string id);
    Task<T> CreateAsync(T entity);
    Task<T?> UpdateAsync(T entity);
    Task<bool> DeleteAsync(string id);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
}

public class Service<T> : IService<T> where T : BaseEntity
{
    protected readonly IRepository<T> _repository;

    public Service(IRepository<T> repository) => _repository = repository;

    public Task<IEnumerable<T>> GetAllAsync() => _repository.GetAllAsync();
    public Task<T?> GetByIdAsync(string id) => _repository.GetByIdAsync(id);
    public Task<T> CreateAsync(T entity) => _repository.CreateAsync(entity);
    public Task<T?> UpdateAsync(T entity) => _repository.UpdateAsync(entity);
    public Task<bool> DeleteAsync(string id) => _repository.DeleteAsync(id);
    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) => _repository.FindAsync(predicate);
}
