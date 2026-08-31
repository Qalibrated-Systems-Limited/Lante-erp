using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Infrastructure.Services;

/// <summary>C5 (P6) — REAL Operations seam. On "create project" from a won deal, creates a Module 5
/// PROJECT in the Operations service, linked to the CRM customer via Project.ClientId. The project lands
/// in Draft (Operations' MD/Finance activation gate still applies). Runs in the request context; mints a
/// per-schema service token carrying the projects.write permission so Operations scopes to the same tenant
/// and passes its authorization policy. Degrades to a no-op result when Operations is not configured.</summary>
public class OperationsProjectGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OperationsProjectGateway> logger) : IProjectGateway
{
    private string? BaseUrl => config["OperationsService:BaseUrl"]?.TrimEnd('/');

    public async Task<ProjectCreationResult> CreateProjectFromDealAsync(DealProjectRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            logger.LogInformation("[Projects seam (disabled)] deal {Deal} → project '{Name}'", request.DealNumber, request.Name);
            return new ProjectCreationResult(true, null, "Operations integration disabled — project not created.");
        }

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new ProjectCreationResult(false, null, "Could not resolve tenant schema for the Operations call.");

        try
        {
            var client = httpClientFactory.CreateClient("OperationsService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "projects.write"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var start = request.Start ?? DateTime.UtcNow;
            var end = request.End is { } e && e > start ? e : start.AddDays(90);
            var name = string.IsNullOrWhiteSpace(request.CustomerName)
                ? $"Deal {request.DealNumber}"
                : $"{request.CustomerName} — {request.DealNumber}";

            var body = new
            {
                name,
                clientName = request.CustomerName,
                clientId = request.CustomerId,             // strong CRM CUSTOMER link (Project.ClientId)
                type = "Sales",
                riskLevel = "Low",
                scopeSummary = $"Auto-created from won deal {request.DealNumber}.",
                contractValue = request.ContractValue,
                plannedBudget = 0m,
                startDate = start,
                expectedEndDate = end,
            };
            var resp = await client.PostAsync($"{BaseUrl}/api/v1/projects",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Operations project create returned {Status} for deal {Deal}: {Err}", resp.StatusCode, request.DealNumber, err);
                return new ProjectCreationResult(false, null, $"Operations rejected the project ({(int)resp.StatusCode}).");
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            string? projectId = null;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                projectId = data.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

            logger.LogInformation("Created Operations project {Id} (Draft) for deal {Deal}", projectId, request.DealNumber);
            return new ProjectCreationResult(true, projectId, $"Draft project created in Operations{(projectId != null ? $" ({projectId})" : "")}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Operations project create failed for deal {Deal}", request.DealNumber);
            return new ProjectCreationResult(false, null, "Could not reach the Operations service.");
        }
    }
}
