using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

public class HseServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HseServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IHseServiceClient
{
    protected override string ClientName => "HseService";
    protected override string ConfigKey => "HseService";

    public Task<HseDashboardDto?> GetDashboardAsync(decimal hoursWorkedYtd) =>
        GetAsync<HseDashboardDto>($"/api/v1/hse-dashboard{BuildQuery(new Dictionary<string, string?> { ["hoursWorkedYtd"] = hoursWorkedYtd.ToString(CultureInfo.InvariantCulture) })}");

    // No date-range/pagination support exists on HSEService's incidents endpoint today —
    // returns everything; the reporting service filters by date client-side.
    public Task<List<HseIncidentReadDto>?> GetAllIncidentsAsync() =>
        GetAsync<List<HseIncidentReadDto>>("/api/v1/hse-incidents");
}
