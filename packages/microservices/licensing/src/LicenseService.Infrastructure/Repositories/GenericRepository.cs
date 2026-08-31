using LicenseService.Core.Entities;
using LicenseService.Core.Interfaces.Repositories;
using LicenseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LicenseService.Infrastructure.Repositories;

public class GenericRepository<T>(LanteLicenseDbContext context) : IGenericRepository<T>
    where T : BaseEntity
{
    protected readonly LanteLicenseDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        if (includeDeleted)
            return await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
        return await DbSet.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await DbSet.ToListAsync();
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
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
        return true;
    }
}
