using ComplianceService.Core.DTOs.Dashboard;

namespace ComplianceService.Core.Interfaces.Services;

public interface IQualityDashboardService
{
    Task<QualityDashboardDto> ComputeAsync();
}
