using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.ServiceClients;

public class ComplianceServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ComplianceServiceClient> logger)
    : BaseServiceClient(httpClientFactory, config, httpContextAccessor, logger), IComplianceServiceClient
{
    protected override string ClientName => "ComplianceService";
    protected override string ConfigKey => "ComplianceService";

    public Task<ComplianceDashboardDto?> GetComplianceDashboardAsync() =>
        GetAsync<ComplianceDashboardDto>("/api/v1/compliance-dashboard");

    public Task<StatutoryDashboardDto?> GetStatutoryDashboardAsync() =>
        GetAsync<StatutoryDashboardDto>("/api/v1/statutory-dashboard");

    public Task<List<PolicyReadDto>?> GetPoliciesAsync() =>
        GetAsync<List<PolicyReadDto>>("/api/v1/compliance-policies");
}
