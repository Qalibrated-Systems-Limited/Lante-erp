using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LicenseService.Infrastructure.Data;

/// <summary>
/// Pins every connection's search_path to "public, licensing" — LanteLicenseDbContext backs
/// LicensesController, a genuine cross-customer platform catalog (see PermissionAuthorizationHandler:
/// only platform.licensing.manage, never seeded into any tenant role, can reach it). It must never
/// resolve into a caller's own tenant schema: this service also provisions a same-named, empty
/// "licenses" table per tenant (via the separate TenantLicenseDbContext, for a driver/equipment-license
/// feature nothing currently implements) — if search_path put the tenant schema first, Postgres would
/// silently shadow the real catalog with that empty table for any caller who happens to have a tenant
/// schema, which is every real user. Previously this interceptor prepended the caller's JWT `schema`
/// claim ahead of "licensing", causing exactly that: platform-catalog reads silently returned empty.
/// </summary>
public class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET search_path TO public, licensing";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET search_path TO public, licensing";
        cmd.ExecuteNonQuery();
    }
}
