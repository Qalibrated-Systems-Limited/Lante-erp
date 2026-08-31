using CrmService.Core.DTOs.Dashboards;

namespace CrmService.Core.Interfaces.Services;

public interface IDashboardService
{
    Task<MdDashboardDto> GetMdPipelineAsync();
    Task<List<SePerformanceDto>> GetSePerformanceAsync(string? periodLabel);
    Task<List<SalesTargetDto>> GetTargetsAsync();
    Task<SalesTargetDto> SaveTargetAsync(SaveSalesTargetDto dto, string userId);
}
