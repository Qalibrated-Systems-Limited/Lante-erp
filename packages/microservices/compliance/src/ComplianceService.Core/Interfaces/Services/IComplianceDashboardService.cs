using ComplianceService.Core.DTOs.Dashboard;

namespace ComplianceService.Core.Interfaces.Services;

public interface IComplianceDashboardService
{
    Task<ComplianceDashboardDto> ComputeAsync();
}
