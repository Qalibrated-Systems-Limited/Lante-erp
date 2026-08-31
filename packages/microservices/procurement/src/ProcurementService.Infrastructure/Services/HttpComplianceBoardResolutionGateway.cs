using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>
/// DEC-C — REAL Compliance seam. Verifies an LPO's Board Resolution against Compliance's COMP-007 register
/// (<c>GET /api/v1/compliance-board-resolutions</c>, a paginated <c>data.items</c> envelope). Read-only:
/// nothing in Compliance is created or changed.
/// <para>The register is fetched once and matched on <b>either the record id or the reference number</b>
/// (case-insensitive), because a user attaching a resolution types the reference off the board minutes rather
/// than a database id.</para>
/// <para>The three outcomes are deliberately distinct — <c>Reachable=false</c> (disabled/unreachable, so the
/// caller falls back to unverified local capture) is not the same as <c>Reachable=true, Found=false</c>
/// (Compliance answered and holds no such resolution, which is a mistyped or fabricated reference and must be
/// rejected). Mints a per-schema service token (compliance.read).</para>
/// </summary>
public class HttpComplianceBoardResolutionGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpComplianceBoardResolutionGateway> logger) : IBoardResolutionGateway
{
    private string? BaseUrl => config["ComplianceService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("Compliance:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    public async Task<BoardResolutionCheck> VerifyAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return new BoardResolutionCheck(false, false, null, null, null, null, null, "No resolution reference supplied.");
        if (!Enabled)
            return new BoardResolutionCheck(false, false, null, null, null, null, null,
                "Compliance is not configured, so the resolution could not be verified.");

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new BoardResolutionCheck(false, false, null, null, null, null, null,
                "No tenant context for the Compliance resolution check.");

        try
        {
            var client = httpClientFactory.CreateClient("ComplianceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "compliance.read"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{BaseUrl}/api/v1/compliance-board-resolutions?pageSize=500", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Compliance board-resolution read returned {Status}.", resp.StatusCode);
                return new BoardResolutionCheck(false, false, null, null, null, null, null,
                    $"Compliance returned {(int)resp.StatusCode} — the resolution could not be verified.");
            }

            var raw = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return new BoardResolutionCheck(true, false, null, null, null, null, null,
                    "Compliance holds no board resolutions.");

            var needle = reference.Trim();
            foreach (var el in items.EnumerateArray())
            {
                var id = Str(el, "id");
                var refNo = Str(el, "referenceNo");
                var hit = string.Equals(id, needle, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(refNo?.Trim(), needle, StringComparison.OrdinalIgnoreCase);
                if (!hit) continue;

                DateTime? date = el.TryGetProperty("resolutionDate", out var d) && d.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(d.GetString(), out var parsed) ? parsed : null;

                logger.LogInformation("Verified board resolution {Ref} against Compliance.", refNo ?? needle);
                return new BoardResolutionCheck(true, true, id, refNo, Str(el, "title"), date,
                    Str(el, "scannedCopyUrl"),
                    $"Verified against Compliance: {refNo}{(date is null ? "" : $" of {date:yyyy-MM-dd}")}.");
            }

            return new BoardResolutionCheck(true, false, null, null, null, null, null,
                $"Compliance holds no board resolution matching '{needle}'.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Compliance board-resolution check failed for {Ref}.", reference);
            return new BoardResolutionCheck(false, false, null, null, null, null, null,
                "Could not reach Compliance to verify the resolution.");
        }
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}

/// <summary>Inert Compliance seam: reports "not reachable" so callers take the DEC-C local-capture fallback and
/// mark the resolution unverified, rather than treating an absent module as a failed check.</summary>
public class NoOpBoardResolutionGateway : IBoardResolutionGateway
{
    public Task<BoardResolutionCheck> VerifyAsync(string reference, CancellationToken ct = default)
        => Task.FromResult(new BoardResolutionCheck(false, false, null, null, null, null, null,
            "Compliance is not wired, so the resolution could not be verified."));
}
