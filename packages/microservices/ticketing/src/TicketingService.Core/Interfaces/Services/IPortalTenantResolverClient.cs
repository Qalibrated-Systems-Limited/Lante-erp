namespace TicketingService.Core.Interfaces.Services;

/// <summary>Resolves the public portal's :slug route segment into the tenant's real schema name,
/// via user-service's internal-key-gated lookup — never trust a client-supplied schema directly.
/// Returns null if the slug is missing, malformed, or doesn't resolve to an active tenant; callers
/// should fall back to the paramless portal behavior (public schema) rather than fail the request,
/// since a stale/mistyped slug shouldn't break an otherwise-valid submission.</summary>
public interface IPortalTenantResolverClient
{
    Task<string?> ResolveSchemaBySlugAsync(string? slug, CancellationToken ct = default);
}
