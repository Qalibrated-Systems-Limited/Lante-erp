using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    Task<(List<AuditLog> Items, int TotalCount)> GetRecentAsync(int page, int pageSize);
}
