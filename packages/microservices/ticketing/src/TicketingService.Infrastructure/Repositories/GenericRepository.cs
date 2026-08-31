using Microsoft.EntityFrameworkCore;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class GenericRepository<T>(TicketingDbContext context) : IGenericRepository<T>
    where T : BaseEntity
{
    protected readonly TicketingDbContext Context = context;
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
