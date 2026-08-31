namespace UserService.Core.DTOs.Audit;

public record AuditLogDto(string Id, string Method, string Path, int StatusCode, string? ActorEmail, DateTime CreatedAt);

public record CreateAuditLogDto(string Method, string Path, int StatusCode, string? ActorEmail, string? ActorId);

public record AuditLogPageDto(List<AuditLogDto> Items, int TotalCount, int Page, int PageSize);
