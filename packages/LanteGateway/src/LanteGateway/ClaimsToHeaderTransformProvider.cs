using System.Text.Json;
using System.Text.RegularExpressions;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace LanteGateway;

/// <summary>
/// Replicates Ocelot's per-route <c>AddHeadersToRequest</c> ("X-Tenant-Id": "Claims[tenant_id] &gt;
/// value") — copies a named JWT claim onto an outgoing request header. Route metadata carries a
/// JSON blob (header name -&gt; Ocelot claim-DSL string) produced by the ocelot.json-&gt;yarp.json
/// conversion; this provider reads it once per route and adds one request transform per header.
/// </summary>
public partial class ClaimsToHeaderTransformProvider : ITransformProvider
{
    private const string MetadataKey = "ClaimsToHeaders";

    [GeneratedRegex(@"^Claims\[(?<claim>[^\]]+)\]\s*>\s*value$")]
    private static partial Regex ClaimsDsl();

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        if (context.Route.Metadata == null || !context.Route.Metadata.TryGetValue(MetadataKey, out var json))
            return;

        var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (mappings == null) return;

        var claimNames = new Dictionary<string, string>(); // header name -> claim type
        foreach (var (header, dsl) in mappings)
        {
            var m = ClaimsDsl().Match(dsl);
            if (m.Success) claimNames[header] = m.Groups["claim"].Value;
        }
        if (claimNames.Count == 0) return;

        context.AddRequestTransform(transformContext =>
        {
            var user = transformContext.HttpContext.User;
            foreach (var (header, claimType) in claimNames)
            {
                var value = user.FindFirst(claimType)?.Value;
                if (string.IsNullOrEmpty(value)) continue;
                transformContext.ProxyRequest.Headers.Remove(header);
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(header, value);
            }
            return default;
        });
    }
}
