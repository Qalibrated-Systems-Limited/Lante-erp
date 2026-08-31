using System.Text.Json;
using System.Web;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Api.Services;

// SUB-005 hard gate: calls HSE's internal RAMS-status endpoint before mobilization is activated.
public class HseServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<HseServiceClient> logger) : IHseServiceClient
{
    public async Task<bool> IsRamsApprovedAsync(string tenantSchema, string subcontractorId, string? siteId)
    {
        var baseUrl = config["HseService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("HseService:BaseUrl not configured — failing closed on RAMS check for subcontractor {SubcontractorId}", subcontractorId);
            return false;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — failing closed on RAMS check for subcontractor {SubcontractorId}", subcontractorId);
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient("HseService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["subcontractorId"] = subcontractorId;
            if (!string.IsNullOrEmpty(siteId)) query["siteId"] = siteId;

            var response = await client.GetAsync($"{baseUrl}/internal/rams-status?{query}");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("HseService returned {StatusCode} checking RAMS status for subcontractor {SubcontractorId}", response.StatusCode, subcontractorId);
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<RamsStatusResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result?.Approved == true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check RAMS status for subcontractor {SubcontractorId} — failing closed", subcontractorId);
            return false;
        }
    }

    private class RamsStatusResponse
    {
        public bool Approved { get; set; }
    }
}
