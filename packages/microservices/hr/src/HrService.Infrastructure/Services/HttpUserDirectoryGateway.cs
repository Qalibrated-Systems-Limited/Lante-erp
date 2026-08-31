using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HrService.Core.DTOs.Org;
using HrService.Core.Interfaces.Services;

namespace HrService.Infrastructure.Services;

/// <summary>
/// H1 — REAL user-service seam. Creates the login account for a new hire (<c>POST /api/v1/users</c>, which
/// also sends the invitation) and reads the Departments and Branches user-service owns (HR-DEC-3). Mints a
/// per-schema service token, scoped per call: <c>users.write</c> to create, <c>departments.manage</c> to read
/// departments (that is the policy user-service puts on its department endpoints).
/// <para>Account creation is <b>fail-open</b>: HR reports the failure and lets it be retried rather than
/// losing an employee record because identity was down. The directory reads degrade to an empty list, which
/// callers treat as "cannot validate" rather than "no such department".</para>
/// </summary>
public class HttpUserDirectoryGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpUserDirectoryGateway> logger) : IUserDirectoryGateway
{
    private string? BaseUrl => config["UserService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("UserService:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
    private string? TenantId => httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;

    public async Task<UserAccountResult> CreateAccountAsync(
        string firstName, string lastName, string email, string? mobile,
        string? departmentId, string? branchId, List<string>? roleIds, CancellationToken ct = default)
    {
        if (!Enabled)
            return new UserAccountResult(false, null, "user-service is not configured — no login account was created.");
        if (string.IsNullOrWhiteSpace(email))
            return new UserAccountResult(false, null, "A work email is required to create a login account.");

        var client = await ClientAsync("users.write");
        if (client is null)
            return new UserAccountResult(false, null, "No tenant context for the user-service account creation.");

        try
        {
            var body = new
            {
                firstName,
                lastName,
                email,
                mobileNumber = mobile ?? string.Empty,
                departmentId,
                branchId,
                tenantId = TenantId,
                roleIds,
            };
            var resp = await client.PostAsync($"{BaseUrl}/api/v1/users",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var reason = ReadMessage(raw);
                logger.LogWarning("user-service account creation returned {Status} for {Email}: {Reason}", resp.StatusCode, email, reason);
                return new UserAccountResult(false, null,
                    reason is null
                        ? $"user-service rejected the account ({(int)resp.StatusCode})."
                        : $"user-service rejected the account: {reason}");
            }

            string? userId = null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("id", out var idEl))
                    userId = idEl.GetString();
            }
            catch (JsonException) { /* created but unparseable — handled below */ }

            if (string.IsNullOrWhiteSpace(userId))
                return new UserAccountResult(false, null, "user-service accepted the request but returned no user id.");

            logger.LogInformation("Created user-service account {UserId} for {Email}.", userId, email);
            return new UserAccountResult(true, userId, $"Login account created and invitation sent to {email}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "user-service account creation failed for {Email} (fail-open — retryable).", email);
            return new UserAccountResult(false, null, "Could not reach user-service to create the login account.");
        }
    }

    public Task<List<OrgUnitDto>> ListDepartmentsAsync(CancellationToken ct = default)
        => ReadUnitsAsync($"{BaseUrl}/api/v1/departments", "departments.manage", "departments", ct);

    public Task<List<OrgUnitDto>> ListBranchesAsync(CancellationToken ct = default)
    {
        // Branches hang off the tenant in user-service, so this needs the tenant id rather than the schema.
        var tenant = TenantId;
        if (string.IsNullOrWhiteSpace(tenant))
        {
            logger.LogInformation("No tenant_id claim — skipping the branch lookup.");
            return Task.FromResult(new List<OrgUnitDto>());
        }
        return ReadUnitsAsync($"{BaseUrl}/api/v1/tenants/{tenant}/branches", "users.read", "branches", ct);
    }

    private async Task<List<OrgUnitDto>> ReadUnitsAsync(string url, string permission, string what, CancellationToken ct)
    {
        var empty = new List<OrgUnitDto>();
        if (!Enabled) return empty;
        var client = await ClientAsync(permission);
        if (client is null) return empty;

        try
        {
            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("user-service {What} read returned {Status}.", what, resp.StatusCode);
                return empty;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data)) return empty;

            // user-service returns either a bare array or a paginated { items: [] }.
            var array = data.ValueKind == JsonValueKind.Array ? data
                : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) ? items
                : default;
            if (array.ValueKind != JsonValueKind.Array) return empty;

            var list = new List<OrgUnitDto>();
            foreach (var el in array.EnumerateArray())
            {
                var id = Str(el, "id");
                var name = Str(el, "name");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
                list.Add(new OrgUnitDto(id!, name!, Str(el, "code"),
                    !el.TryGetProperty("isActive", out var a) || a.ValueKind != JsonValueKind.False));
            }
            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "user-service {What} read failed.", what);
            return empty;
        }
    }

    private async Task<HttpClient?> ClientAsync(string permission)
    {
        var schema = Schema;
        if (string.IsNullOrWhiteSpace(schema)) return null;
        var client = httpClientFactory.CreateClient("UserService");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", permission));
        client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);
        if (!string.IsNullOrWhiteSpace(TenantId)) client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId);
        return client;
    }

    private static string? ReadMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) || raw.TrimStart()[0] != '{' ? "{}" : raw);
            return doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() : null;
        }
        catch { return null; }
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}

/// <summary>Inert user-service seam: no account is created and the directory is empty, so HR still onboards
/// employees (recording that the account is outstanding) when identity is not wired.</summary>
public class NoOpUserDirectoryGateway(ILogger<NoOpUserDirectoryGateway> logger) : IUserDirectoryGateway
{
    public Task<UserAccountResult> CreateAccountAsync(
        string firstName, string lastName, string email, string? mobile,
        string? departmentId, string? branchId, List<string>? roleIds, CancellationToken ct = default)
    {
        logger.LogInformation("user-service not wired — no login account created for {Email}.", email);
        return Task.FromResult(new UserAccountResult(false, null, "user-service is not wired — no login account was created."));
    }

    public Task<List<OrgUnitDto>> ListDepartmentsAsync(CancellationToken ct = default) => Task.FromResult(new List<OrgUnitDto>());
    public Task<List<OrgUnitDto>> ListBranchesAsync(CancellationToken ct = default) => Task.FromResult(new List<OrgUnitDto>());
}
