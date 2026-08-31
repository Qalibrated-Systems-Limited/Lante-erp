namespace StoreService.Core.Entities;

/// Field-level audit trail (#216) — recorded from FinanceAuditInterceptor's pattern: an EF
/// SaveChanges interceptor sees every entity's original and current values, so it records what
/// actually changed rather than only that an endpoint was hit.
public class StoreAuditLog : BaseEntity
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
