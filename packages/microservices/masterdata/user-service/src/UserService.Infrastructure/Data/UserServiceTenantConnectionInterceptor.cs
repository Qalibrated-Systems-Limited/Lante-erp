using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace UserService.Infrastructure.Data;

/// <summary>
/// Phase 3 Increment 2: binds the control-plane context's connection to the caller's tenant schema
/// via Postgres <c>search_path</c>, read from the JWT <c>schema</c> claim (or gateway <c>X-Tenant-Schema</c>
/// header). Control-plane tables are pinned to <c>public</c> in the model, so they resolve there
/// regardless; tenant-plane tables are unqualified and follow the search_path.
///
/// <para>No claim (login/pre-auth, or a platform admin with no tenant) → no SET → default search_path
/// (public). So authentication and platform operations continue to read/write public.</para>
/// </summary>
public partial class UserServiceTenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public UserServiceTenantConnectionInterceptor(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        => await ApplyAsync(connection, cancellationToken);

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    private string? ResolveSchema()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return null;

        // JWT claim wins over the header. The X-Tenant-Schema header is honored only when the
        // caller also proves it's an internal/service-key caller (valid X-Internal-Key) —
        // anonymous end users hitting [AllowAnonymous] routes (e.g. login, public tenant lookup)
        // can set arbitrary headers on their own request, and the gateway doesn't strip them, so
        // "no JWT claim" alone is not enough to trust the header.
        var claimSchema = ctx.User.FindFirst("schema")?.Value;
        var schema = claimSchema ?? (HasValidInternalKey(ctx) ? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault() : null);

        if (string.IsNullOrEmpty(schema) || schema == "public" || !SafeSchemaRegex().IsMatch(schema))
            return null;
        return schema;
    }

    private bool HasValidInternalKey(HttpContext ctx)
    {
        var expected = _configuration["InternalServices:ServiceKey"];
        if (string.IsNullOrEmpty(expected)) return false;
        var provided = ctx.Request.Headers["X-Internal-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(provided)) return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided), System.Text.Encoding.UTF8.GetBytes(expected));
    }

    // ALWAYS set search_path (tenant schema when claimed, else reset to public). Pooled connections
    // retain prior session state, so skipping the SET on a no-claim request would leak the previous
    // caller's tenant search_path — reading their schema. Resetting to public prevents that.
    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        var schema = ResolveSchema();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = schema is null ? "SET search_path TO public" : $"SET search_path TO \"{schema}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void Apply(DbConnection connection)
    {
        var schema = ResolveSchema();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = schema is null ? "SET search_path TO public" : $"SET search_path TO \"{schema}\"";
        cmd.ExecuteNonQuery();
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
