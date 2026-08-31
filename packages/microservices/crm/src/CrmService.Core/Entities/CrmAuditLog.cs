namespace CrmService.Core.Entities;

/// Field-level audit trail (#216) — same pattern as finance's FinanceAuditInterceptor: an EF
/// SaveChanges interceptor sees every entity's original and current values, so it records what
/// actually changed rather than only that an endpoint was hit.
public class CrmAuditLog : BaseEntity
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
