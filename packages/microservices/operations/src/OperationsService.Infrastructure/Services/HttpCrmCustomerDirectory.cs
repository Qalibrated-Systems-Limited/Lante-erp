using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O1/O6 — REAL CRM seam. Verifies a project's linked CRM customer exists (O1 "CUSTOMER verify") and
/// reports a calibration's next-due date so CRM can schedule a client recall reminder (O6). Runs in the
/// request context; mints a per-schema service token so crm-service scopes to the same tenant and passes
/// its authorization policies. Config-gated on <c>Crm:Enabled</c> + <c>CrmService:BaseUrl</c> — degrades to
/// the permissive no-op behaviour (verify passes, recall skipped) when CRM is not configured, so operations
/// is never blocked by CRM being absent.
/// </summary>
public class HttpCrmCustomerDirectory(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpCrmCustomerDirectory> logger) : ICrmCustomerDirectory
{
    private string? BaseUrl => config["CrmService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("Crm:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;

    public async Task<bool> CustomerExistsAsync(string crmLeadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(crmLeadId)) return true;
        if (!Enabled)
            return true;   // verification disabled — never block project creation

        var schema = Schema;
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogWarning("CRM verify for {LeadId}: no tenant schema on the request — treating as valid.", crmLeadId);
            return true;   // can't scope the call — fail open
        }

        try
        {
            var client = httpClientFactory.CreateClient("CrmService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "crm.read.own"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{BaseUrl}/api/v1/customers/{crmLeadId}", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return false;   // CRM reachable and the customer definitively does not exist
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("CRM verify for {LeadId} returned {Status} — treating as valid.", crmLeadId, resp.StatusCode);
                return true;    // CRM error — fail open rather than block the project
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM verify for {LeadId} failed — treating as valid.", crmLeadId);
            return true;        // CRM unreachable — fail open
        }
    }

    public async Task ReportCalibrationDueAsync(CalibrationDueNotice notice, string? schema = null, CancellationToken ct = default)
    {
        if (!Enabled)
        {
            logger.LogInformation("[CRM recall (disabled)] cert {Cert} for {Client} next due {Due:yyyy-MM-dd}.",
                notice.CertificateNumber, notice.ClientName ?? "n/a", notice.NextDueDate);
            return;
        }

        schema ??= Schema;   // background sweeps pass it explicitly; requests fall back to the token
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogWarning("CRM recall for cert {Cert}: no tenant schema available — skipping.", notice.CertificateNumber);
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("CrmService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "crm.write"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var body = new
            {
                certificateNumber = notice.CertificateNumber,
                clientName = notice.ClientName,
                clientEmail = notice.ClientEmail,
                nextDueDate = notice.NextDueDate,
            };
            var resp = await client.PostAsync($"{BaseUrl}/api/v1/aftersales/calibration-recall",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("CRM recall for cert {Cert} returned {Status}.", notice.CertificateNumber, resp.StatusCode);
            else
                logger.LogInformation("Reported calibration recall for cert {Cert} (due {Due:yyyy-MM-dd}) to CRM.",
                    notice.CertificateNumber, notice.NextDueDate);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM recall for cert {Cert} failed (best-effort).", notice.CertificateNumber);
        }
    }

    public async Task<string?> ResolveCustomerIdAsync(string? email, string? name, string? schema = null, CancellationToken ct = default)
    {
        // Email is the only key worth anchoring on. Resolving by name here would bake an ambiguous
        // match into the service request permanently, which is worse than having no id at all.
        if (!Enabled || string.IsNullOrWhiteSpace(email)) return null;

        schema ??= Schema;
        if (string.IsNullOrWhiteSpace(schema)) return null;

        try
        {
            var client = await AuthorizedAsync(schema, "crm.read.own");

            // duplicate-check is the only CRM read that matches an email *exactly*. The customers
            // list endpoint takes "search", not "q", and an unrecognised parameter is ignored
            // rather than rejected — it would hand back the unfiltered first page, which looks
            // like a match whenever the tenant happens to have one customer.
            // Email only: passing a name too would OR them together and match the wrong customer.
            var resp = await client.GetAsync(
                $"{BaseUrl}/api/v1/customers/duplicate-check?email={Uri.EscapeDataString(email)}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("matches", out var matches) ||
                matches.ValueKind != JsonValueKind.Array ||
                matches.GetArrayLength() != 1)
                return null;

            // Belt and braces: confirm the returned customer really carries this address before
            // anchoring to it. A wrong anchor is silent and sticky.
            var hit = matches[0];
            var hitEmail = hit.TryGetProperty("email", out var he) ? he.GetString() : null;
            if (!string.Equals(hitEmail, email, StringComparison.OrdinalIgnoreCase)) return null;

            var id = hit.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (!string.IsNullOrWhiteSpace(id))
                logger.LogInformation("Anchored CRM customer {CrmId} for {Email}.", id, email);
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM customer resolve for '{Email}' failed — leaving the request unanchored.", email);
            return null;
        }
    }

    public async Task<string?> GetCustomerEmailByIdAsync(string crmCustomerId, string? schema = null, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(crmCustomerId)) return null;

        schema ??= Schema;
        if (string.IsNullOrWhiteSpace(schema)) return null;

        try
        {
            var client = await AuthorizedAsync(schema, "crm.read.own");
            var resp = await client.GetAsync($"{BaseUrl}/api/v1/customers/{crmCustomerId}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM email lookup for customer {CrmId} failed — caller will fall back.", crmCustomerId);
            return null;
        }
    }

    private async Task<HttpClient> AuthorizedAsync(string schema, string permission)
    {
        var client = httpClientFactory.CreateClient("CrmService");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", permission));
        client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);
        return client;
    }


    public async Task<string?> GetCustomerEmailAsync(string clientName, string? schema = null, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(clientName)) return null;

        schema ??= Schema;
        if (string.IsNullOrWhiteSpace(schema)) return null;

        try
        {
            var client = httpClientFactory.CreateClient("CrmService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "crm.read.own"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            // duplicate-check matches the name exactly; the list endpoint's "search" is a substring
            // match, which is far too loose to pick a recall recipient from.
            var resp = await client.GetAsync(
                $"{BaseUrl}/api/v1/customers/duplicate-check?name={Uri.EscapeDataString(clientName)}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("matches", out var matches) ||
                matches.ValueKind != JsonValueKind.Array ||
                // Only trust an unambiguous hit. Two customers sharing a name means we cannot tell
                // which one the certificate belongs to, and mailing a recall to the wrong client
                // discloses that the right one holds a certificate — better to fall back.
                matches.GetArrayLength() != 1)
                return null;

            var email = matches[0].TryGetProperty("email", out var e) ? e.GetString() : null;
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM email lookup for '{Client}' failed — caller will fall back.", clientName);
            return null;
        }
    }
}
