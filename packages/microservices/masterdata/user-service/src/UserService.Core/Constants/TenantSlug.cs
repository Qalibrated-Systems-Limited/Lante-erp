using System.Text.RegularExpressions;

namespace UserService.Core.Constants;

/// <summary>
/// Rules for tenant slugs. A slug doubles as the tenant subdomain
/// (<c>&lt;slug&gt;.qalibrated.co.ke</c>) and the basis for its Postgres schema name.
/// </summary>
public static partial class TenantSlug
{
    // Hostnames reserved for the platform / infra — cannot be claimed by a tenant.
    public static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "support", "api", "app", "admin", "mail", "platform",
        "static", "assets", "cdn", "help", "docs", "status", "dashboard", "portal",
    };

    // Lowercase, starts with a letter, alphanumeric with single internal hyphens, 2–50 chars.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex FormatRegex();

    public static bool IsValidFormat(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) &&
        slug.Length is >= 2 and <= 50 &&
        FormatRegex().IsMatch(slug);

    public static bool IsReserved(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && Reserved.Contains(slug.Trim());

    /// <summary>Deterministic Postgres schema name for a slug, e.g. "acme-corp" → "tenant_acme_corp".</summary>
    public static string ToSchemaName(string slug) =>
        "tenant_" + slug.Trim().ToLowerInvariant().Replace('-', '_');
}
