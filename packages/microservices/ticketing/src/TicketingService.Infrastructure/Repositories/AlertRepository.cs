using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class AlertRepository(TicketingDbContext context) : IAlertRepository
{
    public async Task<IEnumerable<Alert>> GetOpenAsync(string tenantId)
        => await context.Alerts
            .Where(a => !a.IsAcknowledged && a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync();

    public async Task<IEnumerable<Alert>> GetHistoryAsync(string tenantId)
        => await context.Alerts
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync();

    public async Task<Alert?> GetByIdAsync(string tenantId, string id)
        => await context.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

    // Keyed on (tenantId, source, title) rather than (source, ticketId): non-ticket-linked
    // sources (e.g. driver license expiry) have no ticketId at all, so keying on that alone would
    // treat every distinct driver as "the same" open alert after the first one, silently dropping
    // the rest. Title already carries the distinguishing subject (ticket title, driver name, etc.)
    // for every source built so far. TenantId is required too, or two tenants with a
    // same-titled ticket (e.g. a generic "General Support" category) would suppress each other.
    public async Task<bool> ExistsOpenAsync(string tenantId, string source, string title)
        => await context.Alerts.AnyAsync(a => a.TenantId == tenantId && a.Source == source && a.Title == title && !a.IsAcknowledged);

    /// <summary>
    /// Inserts the alert, treating "an identical open alert already exists" as success rather than an
    /// error.
    ///
    /// <para>The partial unique index on (TenantId, Source, Title) WHERE not acknowledged and not
    /// deleted is what actually closes the dedup race — AlertService checks then inserts, and at two
    /// replicas both checks pass (#219). But an index alone converts a duplicate from a double alert
    /// into an unhandled 500 on a background worker, which is a different bug rather than a fix. The
    /// duplicate is exactly the outcome the caller wanted: an alert for this condition exists.</para>
    ///
    /// <para>Returns false when the insert was a no-op, so the caller can skip the notification —
    /// otherwise the index would stop the second ALERT while still sending the second EMAIL.</para>
    /// </summary>
    public async Task<bool> AddAsync(Alert alert)
    {
        context.Alerts.Add(alert);
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        // Deliberately narrow: only a recognised Postgres unique violation on the open-alert index is
        // swallowed. Anything else propagates — swallowing broadly here would hide real write failures on
        // a background worker, which is how a queue silently stops working.
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Detach so the failed insert does not poison the next SaveChanges on this context.
            context.Entry(alert).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>Postgres's unique_violation SQLSTATE.</summary>
    internal const string UniqueViolation = "23505";

    /// <summary>The index whose violation means "an identical open alert already exists".</summary>
    internal const string OpenAlertIndex = "IX_Alerts_TenantId_Source_Title";

    /// <summary>
    /// Whether a failed save is the open-alert duplicate, and therefore the caller's desired outcome
    /// rather than an error.
    ///
    /// <para>Kept as a pure function over SQLSTATE and constraint name so it can be tested without a
    /// database and without this project taking a dependency on a test-only provider. Matched on the
    /// SQLSTATE rather than the message, which is localised and version-dependent, and narrowed by
    /// constraint name so an unrelated unique violation is never swallowed as a duplicate alert.</para>
    ///
    /// <para>A null constraint name is accepted because not every Npgsql path populates it; a violation
    /// reaching this repository can only come from the Alerts table it writes.</para>
    /// </summary>
    internal static bool IsOpenAlertDuplicate(string? sqlState, string? constraintName) =>
        sqlState == UniqueViolation && constraintName is null or OpenAlertIndex;

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && IsOpenAlertDuplicate(pg.SqlState, pg.ConstraintName);

    public async Task MarkSeenAsync(string tenantId, string id, string userId)
    {
        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
        // First-seen wins — don't overwrite who/when it was originally noticed.
        if (alert == null || alert.IsSeen) return;
        alert.IsSeen = true;
        alert.SeenBy = userId;
        alert.SeenAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task AcknowledgeAsync(string tenantId, string id, string userId)
    {
        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
        if (alert == null) return;
        alert.IsAcknowledged = true;
        alert.AcknowledgedBy = userId;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
