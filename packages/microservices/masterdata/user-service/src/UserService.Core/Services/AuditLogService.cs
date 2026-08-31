using UserService.Core.DTOs.Audit;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class AuditLogService(IAuditLogRepository repository) : IAuditLogService
{
    public async Task<AuditLogDto> CreateAsync(CreateAuditLogDto dto)
    {
        var entry = new AuditLog
        {
            Method = dto.Method,
            Path = dto.Path,
            StatusCode = dto.StatusCode,
            ActorEmail = dto.ActorEmail,
            ActorId = dto.ActorId,
        };
        await repository.CreateAsync(entry);
        return ToDto(entry);
    }

    public async Task<AuditLogPageDto> GetRecentAsync(int page, int pageSize)
    {
        var (items, totalCount) = await repository.GetRecentAsync(page, pageSize);
        return new AuditLogPageDto(items.Select(ToDto).ToList(), totalCount, page, pageSize);
    }

    private static AuditLogDto ToDto(AuditLog a) => new(a.Id, a.Method, a.Path, a.StatusCode, a.ActorEmail, a.CreatedAt);
}
