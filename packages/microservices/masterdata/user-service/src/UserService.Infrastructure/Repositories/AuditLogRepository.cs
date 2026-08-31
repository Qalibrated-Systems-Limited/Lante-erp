using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class AuditLogRepository(LanteUserServiceDbContext context)
    : GenericRepository<AuditLog>(context), IAuditLogRepository
{
    public async Task<(List<AuditLog> Items, int TotalCount)> GetRecentAsync(int page, int pageSize)
    {
        var query = Context.AuditLogs.OrderByDescending(a => a.CreatedAt);
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }
}
