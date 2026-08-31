using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using HSEService.Core.DTOs.Common;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.Repositories;

// The ONE repository implementation for every HSE entity — registered as an open generic in
// InfrastructureServiceRegistration, so adding a new entity never means adding a new repository.
public class GenericRepository<T>(HSEDbContext context) : IGenericRepository<T>
    where T : BaseEntity
{
    protected readonly HSEDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        if (includeDeleted)
            return await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
        return await DbSet.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<T>> GetAllAsync() => await DbSet.ToListAsync();

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await DbSet.Where(predicate).ToListAsync();

    public async Task<PaginatedResult<T>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = DbSet.AsQueryable();
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    public async Task<T> CreateAsync(T entity)
    {
        await DbSet.AddAsync(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        DbSet.Update(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await DbSet.FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(string id)
    {
        var entity = await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) return false;
        entity.IsDeleted = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
        return true;
    }
}
