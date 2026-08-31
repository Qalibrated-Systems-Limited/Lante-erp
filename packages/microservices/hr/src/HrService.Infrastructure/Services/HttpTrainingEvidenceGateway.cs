using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HrService.Core.Interfaces.Services;

namespace HrService.Infrastructure.Services;

/// <summary>
/// H7 — REAL mandatory-training evidence seam (HR-DEC-5). Reads
/// <c>GET /api/v1/hse-training-records</c> and <c>GET /api/v1/compliance-abc-training</c> with a per-schema
/// service token, so HR can say whether someone's HSE or anti-bribery training is in date without keeping a
/// second copy of a record another module owns.
/// <para><b>A failure returns null, never an empty list.</b> The distinction matters more here than anywhere
/// else in HR: an empty list means "nobody has done this training", while null means "we could not ask". If a
/// network error were reported as the former, an unreachable hse-service would silently mark the whole company
/// non-compliant and block every salary increment.</para>
/// </summary>
public class HttpTrainingEvidenceGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpTrainingEvidenceGateway> logger) : ITrainingEvidenceGateway
{
    private string? HseUrl => config["HseService:BaseUrl"]?.TrimEnd('/');
    private string? ComplianceUrl => config["ComplianceService:BaseUrl"]?.TrimEnd('/');
    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;

    public Task<List<TrainingEvidenceDto>?> ListHseTrainingAsync(IEnumerable<string> employeeUserIds, CancellationToken ct = default)
        => ReadAsync(HseUrl, "HseService", "api/v1/hse-training-records", "hse.read", "HSE training", employeeUserIds, ct);

    public Task<List<TrainingEvidenceDto>?> ListAntiBriberyTrainingAsync(IEnumerable<string> employeeUserIds, CancellationToken ct = default)
        => ReadAsync(ComplianceUrl, "ComplianceService", "api/v1/compliance-abc-training", "compliance.read", "anti-bribery training", employeeUserIds, ct);

    private async Task<List<TrainingEvidenceDto>?> ReadAsync(
        string? baseUrl, string clientName, string path, string permission, string what,
        IEnumerable<string> employeeUserIds, CancellationToken ct)
    {
        var wanted = employeeUserIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet();
        if (wanted.Count == 0) return [];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogInformation("{What} source is not configured — compliance cannot be checked.", what);
            return null;
        }
        var schema = Schema;
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogInformation("No tenant schema on the request — skipping the {What} read.", what);
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", permission));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            // Both controllers page; ask for a page big enough to cover a company of this size in one hop.
            var resp = await client.GetAsync($"{baseUrl}/{path}?pageSize=500", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("{What} read returned {Status} for {Schema}.", what, resp.StatusCode, schema);
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;

            // Either a bare array or a paginated { items: [] } — both shapes exist across the estate.
            var array = data.ValueKind == JsonValueKind.Array ? data
                : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) ? items
                : default;
            if (array.ValueKind != JsonValueKind.Array) return null;

            var list = new List<TrainingEvidenceDto>();
            foreach (var el in array.EnumerateArray())
            {
                var userId = Str(el, "employeeUserId");
                if (string.IsNullOrWhiteSpace(userId) || !wanted.Contains(userId)) continue;
                var completed = Date(el, "completedOn");
                if (completed is null) continue;

                // hse states an expiry; compliance states the next due date. Same fact, different name.
                var expires = Date(el, "expiresOn") ?? Date(el, "nextDueOn");
                list.Add(new TrainingEvidenceDto(userId!, Str(el, "course") ?? what, completed.Value, expires, Str(el, "certificateUrl")));
            }

            logger.LogInformation("Read {Count} {What} record(s) for {Schema}.", list.Count, what, schema);
            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{What} read failed for {Schema} — reported as unknown, not as non-compliant.", what, schema);
            return null;
        }
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTime? Date(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            && DateTime.TryParse(p.GetString(), out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
}

/// <summary>Inert evidence seam: every read reports "could not check" rather than "not done", so an unwired
/// tenant never has its staff marked non-compliant by omission.</summary>
public class NoOpTrainingEvidenceGateway(ILogger<NoOpTrainingEvidenceGateway> logger) : ITrainingEvidenceGateway
{
    public Task<List<TrainingEvidenceDto>?> ListHseTrainingAsync(IEnumerable<string> employeeUserIds, CancellationToken ct = default)
    {
        logger.LogInformation("hse-service not wired — HSE training compliance cannot be checked.");
        return Task.FromResult<List<TrainingEvidenceDto>?>(null);
    }

    public Task<List<TrainingEvidenceDto>?> ListAntiBriberyTrainingAsync(IEnumerable<string> employeeUserIds, CancellationToken ct = default)
    {
        logger.LogInformation("compliance-service not wired — anti-bribery compliance cannot be checked.");
        return Task.FromResult<List<TrainingEvidenceDto>?>(null);
    }
}
