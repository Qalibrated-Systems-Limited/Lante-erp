using UserService.Core.DTOs.Audit;

namespace UserService.Core.Interfaces.Services;

public interface IAuditLogService
{
    Task<AuditLogDto> CreateAsync(CreateAuditLogDto dto);
    Task<AuditLogPageDto> GetRecentAsync(int page, int pageSize);
}
