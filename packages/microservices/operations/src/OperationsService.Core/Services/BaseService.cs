using AutoMapper;
using OperationsService.Core.Entities;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public abstract class BaseService<TEntity, TRead, TCreate, TUpdate>
    : IGenericService<TRead, TCreate, TUpdate>
    where TEntity : BaseEntity, new()
{
    protected readonly IGenericRepository<TEntity> Repository;
    protected readonly IMapper Mapper;

    protected BaseService(IGenericRepository<TEntity> repository, IMapper mapper)
    {
        Repository = repository;
        Mapper = mapper;
    }

    public virtual async Task<TRead?> GetByIdAsync(string id)
    {
        var entity = await Repository.GetByIdAsync(id);
        return entity is null ? default : Mapper.Map<TRead>(entity);
    }

    public virtual async Task<TRead> CreateAsync(TCreate dto, string userId)
    {
        var entity = Mapper.Map<TEntity>(dto);
        entity.CreatedBy = userId;
        entity.UpdatedBy = userId;
        var created = await Repository.CreateAsync(entity);
        return Mapper.Map<TRead>(created);
    }

    public virtual async Task<TRead> UpdateAsync(string id, TUpdate dto, string userId)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} {id} not found.");
        Mapper.Map(dto, entity);
        entity.UpdatedBy = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        var updated = await Repository.UpdateAsync(entity);
        return Mapper.Map<TRead>(updated);
    }

    public virtual async Task DeleteAsync(string id, string userId)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} {id} not found.");
        entity.IsDeleted = true;
        entity.UpdatedBy = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        await Repository.UpdateAsync(entity);
    }
}
