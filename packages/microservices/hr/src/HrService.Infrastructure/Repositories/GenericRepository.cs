using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using HrService.Core.Entities;
using HrService.Core.Interfaces.Repositories;
using HrService.Infrastructure.Data;

namespace HrService.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly HrDbContext Context;
    protected readonly DbSet<T> DbSet;

    public GenericRepository(HrDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(string id) =>
        await DbSet.FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null)
    {
        IQueryable<T> query = DbSet;
        if (filter != null) query = query.Where(filter);
        return await query.ToListAsync();
    }

    public async Task<T> CreateAsync(T entity)
    {
        await DbSet.AddAsync(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(T entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        DbSet.Update(entity);
        await Context.SaveChangesAsync();
    }

    public async Task RestoreAsync(T entity)
    {
        entity.IsDeleted = false;
        entity.UpdatedAt = DateTime.UtcNow;
        DbSet.Update(entity);
        await Context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string id) =>
        await DbSet.FindAsync(id) is not null;

    public IQueryable<T> Query() => DbSet.AsQueryable();
}
