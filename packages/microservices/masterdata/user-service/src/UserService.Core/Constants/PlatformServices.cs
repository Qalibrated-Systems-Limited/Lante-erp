using System.Text.Json;

namespace UserService.Core.Constants;

/// <summary>
/// Canonical keys for the Lante backend services that receive a per-tenant schema.
/// "user" is the identity/control service and is always provisioned; the rest are the
/// business services, narrowed per subscription plan (see <see cref="ResolveSubscribed"/>).
/// </summary>
public static class PlatformServices
{
    public const string User       = "user";
    public const string Ticketing  = "ticketing";
    public const string Operations = "operations";
    public const string Crm        = "crm";
    public const string Procurement = "procurement";
    public const string Finance    = "finance";
    public const string Fleet      = "fleet";
    public const string Licensing  = "licensing";
    public const string Stores     = "stores";
    public const string Hse           = "hse";
    public const string Compliance    = "compliance";
    public const string Subcontracts  = "subcontracts";
    public const string Reporting     = "reporting";
    public const string Hr            = "hr";

    /// <summary>Every service key.</summary>
    public static readonly IReadOnlyList<string> All =
        new[] { User, Ticketing, Operations, Crm, Procurement, Finance, Fleet, Licensing, Stores, Hse, Compliance, Subcontracts, Reporting, Hr };

    /// <summary>The optional business services, gated by the subscription plan.</summary>
    public static readonly IReadOnlyList<string> Business =
        new[] { Ticketing, Operations, Crm, Procurement, Finance, Fleet, Licensing, Stores, Hse, Compliance, Subcontracts, Reporting, Hr };

    /// <summary>
    /// Resolves which services a tenant is provisioned into from its plan's FeaturesJson
    /// (a JSON array of slugs, e.g. <c>["ticketing","fleet"]</c>). "user" is always included;
    /// business services are included only when named in the plan. Unknown slugs are ignored.
    /// A null/empty/invalid plan yields user-only.
    /// </summary>
    public static IReadOnlyList<string> ResolveSubscribed(string? featuresJson)
    {
        var result = new List<string> { User };
        if (string.IsNullOrWhiteSpace(featuresJson))
            return result;

        try
        {
            var slugs = JsonSerializer.Deserialize<List<string>>(featuresJson);
            if (slugs is null) return result;

            var set = slugs
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet();

            result.AddRange(Business.Where(set.Contains));
        }
        catch (JsonException)
        {
            // Malformed FeaturesJson → provision the control service only.
        }

        return result;
    }
}
