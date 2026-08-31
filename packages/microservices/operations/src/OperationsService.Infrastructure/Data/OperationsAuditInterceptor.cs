using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OperationsService.Core.Entities;

namespace OperationsService.Infrastructure.Data;

/// <summary>
/// Records every operations write into <see cref="OperationsAuditLog"/> — field-level, in the same
/// transaction as the change (#216). <see cref="CalibrationAuditLog"/> is untouched: it is a
/// purpose-built ISO-17025 event trail for a specific workflow, not a generic change log, and this
/// interceptor covers everything else (and would double-log calibration writes if it didn't exclude
/// that entity too).
/// </summary>
public class OperationsAuditInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
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
            .Where(e => e.Entity is not OperationsAuditLog and not CalibrationAuditLog)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            db.Add(new OperationsAuditLog
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

    private string? ResolveActor()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.User is null) return null;

        return ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.Email)?.Value;
    }
}
