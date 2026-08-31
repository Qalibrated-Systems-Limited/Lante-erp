using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

public class OperationsServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OperationsServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IOperationsServiceClient
{
    protected override string ClientName => "OperationsService";
    protected override string ConfigKey => "OperationsService";

    public Task<PagedResult<ProjectReadDto>?> GetProjectsPageAsync(int page, int pageSize) =>
        GetAsync<PagedResult<ProjectReadDto>>($"/api/v1/projects{BuildQuery(new Dictionary<string, string?> { ["Page"] = page.ToString(), ["PageSize"] = pageSize.ToString() })}");

    public Task<BudgetSummaryDto?> GetProjectBudgetAsync(string projectId) =>
        GetAsync<BudgetSummaryDto>($"/api/v1/projects/{Uri.EscapeDataString(projectId)}/budget");
}
