using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface IAlertRepository
{
    // Alert is a single shared table spanning every tenant (it has its own TenantId column,
    // unlike schema-per-tenant tables such as Tickets), so every read here MUST filter by the
    // caller's tenant explicitly — there's no Postgres schema boundary doing it for us.
    // Returns every open/historical alert in the tenant; the caller (AlertsController) applies
    // per-user visibility (system.admin, personal assignment, RequiredPermission, or the legacy
    // alerts.read.all gate for alerts with neither) in-memory, since the set is already capped
    // at 200 rows and visibility depends on JWT permission claims a repository shouldn't know about.
    Task<IEnumerable<Alert>> GetOpenAsync(string tenantId);
    Task<IEnumerable<Alert>> GetHistoryAsync(string tenantId);
    // Tenant-scoped at the query level (defense-in-depth): an id from another tenant returns
    // null rather than relying on callers to compare TenantId themselves.
    Task<Alert?> GetByIdAsync(string tenantId, string id);
    // Dedup guard — background jobs re-scan the same still-broken condition on every run
    // (e.g. every 5 min for SLA), so callers must skip creating a new row while an
    // unacknowledged one already exists for the same source+title, or it spams duplicates.
    // Scoped by tenant too, or two tenants with a same-titled ticket would suppress each other.
    Task<bool> ExistsOpenAsync(string tenantId, string source, string title);
    /// <summary>Inserts the alert. Returns false when an identical OPEN alert already existed, so the
    /// caller can skip notifying — the partial unique index closes the dedup race (#219), but the
    /// notification has to be skipped too or the second email still goes out.</summary>
    Task<bool> AddAsync(Alert alert);
    // Writes are tenant-scoped too — a cross-tenant id is a silent no-op at the data layer,
    // even if a controller-level guard is ever bypassed or forgotten.
    Task MarkSeenAsync(string tenantId, string id, string userId);
    Task AcknowledgeAsync(string tenantId, string id, string userId);
}
