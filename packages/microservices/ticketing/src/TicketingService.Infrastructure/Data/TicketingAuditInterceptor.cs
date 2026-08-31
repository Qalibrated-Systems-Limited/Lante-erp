using System.Security.Claims;
using System.Text;
using TicketingService.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TicketingService.Infrastructure.Data;

/// <summary>
/// Records every ticketing write into <see cref="TicketingAuditLog"/> — same shape as finance's
/// <c>FinanceAuditInterceptor</c> (#216): intercepting SaveChanges rather than calling a logger at
/// each site means a new entity or a new controller is covered without anyone remembering to add
/// it, and the audit row is written in the same transaction as the change it describes.
///
/// <para>Distinct from <c>TicketHistory</c>, which is a user-facing "what happened to this ticket"
/// timeline (status changes, comments, assignments) populated deliberately at specific call sites.
/// This is the field-level "what changed in the database" record, covering every entity — including
/// the ones nothing writes an explicit history entry for.</para>
/// </summary>
public class TicketingAuditInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private const int MaxDetailLength = 2000;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Record(DbContext? db)
    {
        if (db is null) return;

        var actor = ResolveActor();

        var entries = db.ChangeTracker.Entries()
            .Where(e => e.Entity is not TicketingAuditLog)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            db.Add(new TicketingAuditLog
            {
                Entity = entry.Metadata.ClrType.Name,
                EntityId = KeyOf(entry),
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated",
                },
                Actor = actor,
                Details = DetailsOf(entry),
                At = DateTime.UtcNow,
                CreatedBy = actor ?? "system",
            });
        }
    }

    private static string KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return string.Empty;
        var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "");
        return string.Join("|", values);
    }

    private static string? DetailsOf(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified) return null;

        var sb = new StringBuilder();
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;

            if (sb.Length > 0) sb.Append("; ");
            sb.Append($"{prop.Metadata.Name}: {Render(prop.OriginalValue)} -> {Render(prop.CurrentValue)}");
            if (sb.Length >= MaxDetailLength) { sb.Append(" …"); break; }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string Render(object? value) => value switch
    {
        null => "(null)",
        DateTime d => d.ToString("O"),
        _ => value.ToString() ?? "",
    };

    /// <summary>The caller, from the JWT rather than any header. Background sweeps have no
    /// HttpContext and record "system".</summary>
    private string? ResolveActor()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.User is null) return null;

        return ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.Email)?.Value;
    }
}
