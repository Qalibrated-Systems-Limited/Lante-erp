using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ComplianceService.Infrastructure.Data;

/// <summary>
/// Binds each opened connection to the caller's tenant schema (JWT <c>schema</c> claim, else the
/// gateway-injected <c>X-Tenant-Schema</c> header for internal/service-key calls with no ASP.NET
/// user identity). Copied verbatim (namespace only) from TicketingService/HSEService.
/// </summary>
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

    private string? Resolve()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return null;

        // JWT claim wins over the header. The X-Tenant-Schema header is honored only when the
        // caller also proves it's an internal/service-key caller (valid X-Internal-Key) —
        // anonymous end users hitting [AllowAnonymous] routes (e.g. the public portal) can set
        // arbitrary headers on their own request, and the gateway doesn't strip them, so "no JWT
        // claim" alone is not enough to trust the header.
        var claimSchema = ctx.User.FindFirst("schema")?.Value;

        string? schema = claimSchema;
        if (claimSchema == null && HasValidInternalKey(ctx))
            schema = ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault();

        if (!string.IsNullOrEmpty(schema) && !SafeSchemaRegex().IsMatch(schema))
            schema = null;

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
        var schema = Resolve();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = string.IsNullOrEmpty(schema) || schema == "public"
            ? "SET search_path TO public" : $"SET search_path TO \"{schema}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void Apply(DbConnection connection)
    {
        var schema = Resolve();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = string.IsNullOrEmpty(schema) || schema == "public"
            ? "SET search_path TO public" : $"SET search_path TO \"{schema}\"";
        cmd.ExecuteNonQuery();
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
