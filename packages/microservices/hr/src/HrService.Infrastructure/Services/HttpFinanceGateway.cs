using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HrService.Core.Interfaces.Services;
using HrService.Core.Services;

namespace HrService.Infrastructure.Services;

/// <summary>
/// H5 — REAL finance seam, read-only. Reads finance's chart of accounts (<c>GET /api/v1/finance/chart-of-accounts</c>)
/// so salary components, statutory rates and deduction types can be mapped to the accounts the H6 payroll journal
/// will post to (P7 step 7.5). Mints a per-schema service token, exactly as the other finance-reading modules do,
/// so finance scopes to the same tenant.
/// <para><b>Read-only on purpose.</b> Finance stays the ledger (HR-DEC-4): HR never writes an account, it picks
/// one. H6 adds the posting side (payroll journal + statutory remittance), which will need a schema-parameterised
/// call because it runs from the payroll-run request and, later, a background sweep — this read is request-scoped
/// and takes the schema from the caller's own token.</para>
/// <para><b>Degrades to an empty list</b> when finance is unconfigured or unreachable. A mapping screen then has
/// nothing to pick rather than failing outright, and anything already mapped keeps its denormalised
/// <c>GlAccountCode</c>/<c>GlAccountName</c>, so existing mappings still read correctly while finance is down.</para>
/// </summary>
public class HttpFinanceGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpFinanceGateway> logger) : IFinanceGateway
{
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;

    public async Task<List<GlAccountDto>> ListAccountsAsync(CancellationToken ct = default)
    {
        var empty = new List<GlAccountDto>();

        var baseUrl = BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogInformation("Finance is not configured — the GL account list is empty.");
            return empty;
        }

        var schema = Schema;
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogInformation("No tenant schema on the request — skipping the finance chart-of-accounts read.");
            return empty;
        }

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.read"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{baseUrl}/api/v1/finance/chart-of-accounts", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance chart-of-accounts read returned {Status} for {Schema}.", resp.StatusCode, schema);
                return empty;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return empty;

            var accounts = new List<GlAccountDto>();
            foreach (var el in data.EnumerateArray())
            {
                var id = Str(el, "id");
                var code = Str(el, "code");
                var name = Str(el, "name");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                    continue;

                accounts.Add(new GlAccountDto(
                    id!, code!, name!,
                    Str(el, "classification"),
                    // Header accounts cannot be posted to; absent flags are read the safe way round — a missing
                    // isDirectPosting means "do not offer it for posting", a missing isActive means "still active".
                    Bool(el, "isDirectPosting", false),
                    Bool(el, "isActive", true)));
            }

            logger.LogInformation("Read {Count} GL accounts from finance for {Schema}.", accounts.Count, schema);
            return accounts;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance chart-of-accounts read failed for {Schema} — mapping falls back to an empty list.", schema);
            return empty;
        }
    }

    public async Task<JournalPostResult> PostJournalAsync(
        string tenantSchema, DateTime entryDate, string description, string sourceDocumentId,
        List<JournalLineDto> lines, CancellationToken ct = default)
    {
        var baseUrl = BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new JournalPostResult(false, null, null, "Finance is not configured — the payroll journal was not posted.");
        if (string.IsNullOrWhiteSpace(tenantSchema))
            return new JournalPostResult(false, null, null, "No tenant schema for the finance posting.");
        if (lines.Count == 0)
            return new JournalPostResult(false, null, null, "The journal has no lines.");

        // Refuse to send an unbalanced journal. Finance would reject it anyway, but a local check names the
        // problem in HR's own terms instead of surfacing a bare 400 from another service.
        var debit = lines.Sum(l => l.Debit);
        var credit = lines.Sum(l => l.Credit);
        if (Money.Round(debit - credit) != 0m)
            return new JournalPostResult(false, null, null,
                $"The payroll journal does not balance: debits {debit:N2} against credits {credit:N2}.");

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, tenantSchema, "system.admin", "finance.write"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var body = new
            {
                entryDate,
                description,
                sourceModule = "HR-Payroll",
                sourceDocumentId,
                postImmediately = true,
                lines = lines.Select(l => new { accountId = l.AccountId, description = l.Description, debit = l.Debit, credit = l.Credit }),
            };
            var resp = await client.PostAsync($"{baseUrl}/api/v1/finance/journals",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance rejected the payroll journal for {Doc} ({Status}): {Body}",
                    sourceDocumentId, resp.StatusCode, raw);
                return new JournalPostResult(false, null, null,
                    $"Finance rejected the payroll journal ({(int)resp.StatusCode}). The run stands — posting can be retried.");
            }

            string? id = null, no = null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    id = Str(data, "id");
                    no = Str(data, "entryNo");
                }
            }
            catch (JsonException) { /* posted but unparseable — reported below */ }

            if (string.IsNullOrWhiteSpace(id))
                return new JournalPostResult(false, null, null,
                    "Finance accepted the journal but returned no entry id, so it could not be linked to the run.");

            logger.LogInformation("Posted payroll journal {EntryNo} ({Id}) for {Doc} in {Schema}.", no, id, sourceDocumentId, tenantSchema);
            return new JournalPostResult(true, id, no, $"Payroll journal {no ?? id} posted to finance.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Payroll journal posting failed for {Doc} in {Schema} — retryable.", sourceDocumentId, tenantSchema);
            return new JournalPostResult(false, null, null,
                "Could not reach finance to post the payroll journal. The run stands — posting can be retried.");
        }
    }

    public async Task<List<OutstandingAdvanceDto>?> ListOutstandingAdvancesAsync(string employeeUserId, CancellationToken ct = default)
    {
        var baseUrl = BaseUrl;
        var schema = Schema;
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(employeeUserId))
            return null;

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.read"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var found = new List<OutstandingAdvanceDto>();

            // Imprest is outstanding once the money is out and not yet fully retired.
            var imprest = await ReadRowsAsync(client, $"{baseUrl}/api/v1/finance/imprest", ct);
            if (imprest is null) return null;
            foreach (var el in imprest)
            {
                if (Str(el, "employeeId") != employeeUserId) continue;
                var status = Str(el, "status") ?? "";
                if (status is not ("Disbursed" or "PartlyRetired")) continue;
                found.Add(new OutstandingAdvanceDto("Imprest", Str(el, "requestNo") ?? Str(el, "id") ?? "", Dec(el, "amount"), status));
            }

            // A personal advance is outstanding until it has been deducted.
            var advances = await ReadRowsAsync(client, $"{baseUrl}/api/v1/finance/imprest/advances", ct);
            if (advances is null) return null;
            foreach (var el in advances)
            {
                if (Str(el, "employeeId") != employeeUserId) continue;
                var status = Str(el, "status") ?? "";
                if (status != "Pending") continue;
                found.Add(new OutstandingAdvanceDto("Advance", Str(el, "reference") ?? Str(el, "id") ?? "", Dec(el, "amount"), status));
            }

            logger.LogInformation("Found {Count} outstanding advance(s) for {User} in {Schema}.", found.Count, employeeUserId, schema);
            return found;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Outstanding-advance read failed for {User} — reported as unknown, not as zero.", employeeUserId);
            return null;
        }
    }

    /// <summary>Reads a finance list endpoint into raw elements. Null means it could not be read — which is
    /// deliberately different from an empty list.</summary>
    private async Task<List<JsonElement>?> ReadRowsAsync(HttpClient client, string url, CancellationToken ct)
    {
        var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Finance read {Url} returned {Status}.", url, resp.StatusCode);
            return null;
        }
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        var array = data.ValueKind == JsonValueKind.Array ? data
            : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) ? items
            : default;
        if (array.ValueKind != JsonValueKind.Array) return null;
        return array.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static decimal Dec(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : 0m;

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool Bool(JsonElement el, string name, bool whenMissing)
        => el.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? p.GetBoolean() : whenMissing;
}

/// <summary>Inert finance seam: no accounts to map. HR still configures salary components and deductions — they
/// simply carry no GL account until finance is wired, which H6 checks for before it will build a payroll journal.</summary>
public class NoOpFinanceGateway(ILogger<NoOpFinanceGateway> logger) : IFinanceGateway
{
    public Task<List<GlAccountDto>> ListAccountsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Finance not wired — no GL accounts available for mapping.");
        return Task.FromResult(new List<GlAccountDto>());
    }

    public Task<JournalPostResult> PostJournalAsync(
        string tenantSchema, DateTime entryDate, string description, string sourceDocumentId,
        List<JournalLineDto> lines, CancellationToken ct = default)
    {
        logger.LogInformation("Finance not wired — payroll journal for {Doc} ({Count} lines) not posted.", sourceDocumentId, lines.Count);
        return Task.FromResult(new JournalPostResult(false, null, null, "Finance is not wired — the payroll journal was not posted."));
    }

    public Task<List<OutstandingAdvanceDto>?> ListOutstandingAdvancesAsync(string employeeUserId, CancellationToken ct = default)
    {
        logger.LogInformation("Finance not wired — outstanding advances cannot be checked for {User}.", employeeUserId);
        return Task.FromResult<List<OutstandingAdvanceDto>?>(null);
    }
}
