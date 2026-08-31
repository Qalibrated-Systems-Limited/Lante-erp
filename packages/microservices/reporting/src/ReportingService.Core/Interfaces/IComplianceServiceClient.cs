using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IComplianceServiceClient
{
    Task<ComplianceDashboardDto?> GetComplianceDashboardAsync();
    Task<StatutoryDashboardDto?> GetStatutoryDashboardAsync();
    Task<List<PolicyReadDto>?> GetPoliciesAsync();
}
