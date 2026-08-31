using System.Security.Claims;
using System.Text;
using FinanceService.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceService.Infrastructure.Data;

/// <summary>
/// Records every finance write into <see cref="FinanceAuditLog"/>.
///
/// <para>That table has existed since finance's <c>InitialCreate</c> and had <b>never recorded a
/// row</b>: the entity and the DbSet were the only two references to it anywhere in the service
/// (#285). It shipped to every tenant schema reading as an audit trail while being permanently
/// empty — and it sat behind the 79 endpoints that enforced no permissions until #277, so for that
/// whole window there is no record of who called what. That period cannot be reconstructed.</para>
///
/// <para><b>An interceptor rather than explicit calls at each site.</b> The alternative was a
/// <c>LogAsync</c> at every approve/post/close path, which is how the other services do it — and it
/// is exactly the shape that gets forgotten on the next endpoint someone adds. Auditing at
/// SaveChanges cannot be omitted by a new controller, because nothing reaches the database without
/// passing through here.</para>
///
/// <para>It also answers the complaint in #216, that the audit log can say which endpoint was called
/// but not what changed: an interceptor sees the entity's original and current values, so a
/// modification records the fields that actually moved rather than only the fact that something did.</para>
/// </summary>
public class FinanceAuditInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    /// <summary>Cap on the per-row change summary. A journal with hundreds of lines must not turn one
    /// audit row into a megabyte of text; the entity and action are the load-bearing parts.</summary>
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

        // The .ToList() is what prevents the loop: the collection is materialised before any audit row
        // is added, so rows appended below are not in it. The FinanceAuditLog filter is a second line
        // that currently cannot fire — deleting it changes no test — and is kept only because the
        // failure it guards against is unbounded recursion rather than cosmetics, and a later refactor
        // that drops the materialisation would otherwise reintroduce it silently.
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.Entity is not FinanceAuditLog)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            db.Add(new FinanceAuditLog
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

    /// <summary>
    /// For a modification, the fields that actually moved and what they moved from. For a create or
    /// delete, nothing — the row itself is the detail.
    ///
    /// <para>The state check below is intent, not mechanism: EF reports <b>zero</b> modified properties
    /// on an Added entity, so the loop would produce nothing on an insert either way (verified — 0 of 15
    /// on a Currency insert). It is kept because relying on that is relying on an EF implementation
    /// detail, and this repo is mid-way through an EF 8→9 bump (#227) where exactly such details move.
    /// Deleting it changes no test, so it is documented as belt rather than braces.</para>
    /// </summary>
    private static string? DetailsOf(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified) return null;

        var sb = new StringBuilder();
        foreach (var prop in entry.Properties)
        {
            // EF's own change detection already excludes same-value assignments: reassigning a
            // property its current value leaves the entity Unchanged and IsModified false. I had a
            // second Equals(original, current) check here and deleting it changed no test outcome,
            // because it could never fire. Removed rather than kept as reassurance.
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

    /// <summary>
    /// The caller, from the JWT rather than any header — a client-suppliable value must not be able to
    /// attribute a write to somebody else. Background sweeps have no HttpContext and record "system",
    /// which is honest: nobody clicked.
    /// </summary>
    private string? ResolveActor()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.User is null) return null;

        return ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.Email)?.Value;
    }
}
