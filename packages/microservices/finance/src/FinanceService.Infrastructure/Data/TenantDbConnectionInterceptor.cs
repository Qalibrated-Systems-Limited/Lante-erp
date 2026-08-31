using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace FinanceService.Infrastructure.Data;

/// Binds each opened connection to the caller's tenant schema via search_path. The JWT `schema`
/// claim wins over the X-Tenant-Schema header (a client-suppliable header must not be able to
/// repoint another tenant's data). Always resets to public when no schema is claimed, since pooled
/// connections retain prior session state.
public partial class TenantDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public TenantDbConnectionInterceptor(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
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
        // anonymous end users hitting [AllowAnonymous] routes (e.g. the public portal) can set
        // arbitrary headers on their own request, and the gateway doesn't strip them, so "no JWT
        // claim" alone is not enough to trust the header.
        var claimSchema = ctx.User.FindFirst("schema")?.Value;
        var schema = claimSchema ?? (HasValidInternalKey(ctx) ? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault() : null);

        if (!string.IsNullOrEmpty(schema) && !SafeSchemaRegex().IsMatch(schema)) schema = null;
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

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        var schema = ResolveSchema();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = string.IsNullOrEmpty(schema) || schema == "public"
            ? "SET search_path TO public" : $"SET search_path TO \"{schema}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void Apply(DbConnection connection)
    {
        var schema = ResolveSchema();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = string.IsNullOrEmpty(schema) || schema == "public"
            ? "SET search_path TO public" : $"SET search_path TO \"{schema}\"";
        cmd.ExecuteNonQuery();
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
