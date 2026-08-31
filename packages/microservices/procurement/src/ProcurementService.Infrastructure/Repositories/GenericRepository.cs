using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Infrastructure.Data;

namespace ProcurementService.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ProcurementDbContext Context;
    protected readonly DbSet<T> DbSet;

    public GenericRepository(ProcurementDbContext context)
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
        DbSet.Remove(entity);
        await Context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string id) =>
        await DbSet.FindAsync(id) is not null;

    public IQueryable<T> Query() => DbSet.AsQueryable();
}
