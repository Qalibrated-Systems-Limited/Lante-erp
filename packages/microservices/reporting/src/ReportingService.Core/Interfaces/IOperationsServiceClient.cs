using ReportingService.Core.DTOs;

namespace ReportingService.Core.Interfaces;

public interface IOperationsServiceClient
{
    Task<PagedResult<ProjectReadDto>?> GetProjectsPageAsync(int page, int pageSize);
    Task<BudgetSummaryDto?> GetProjectBudgetAsync(string projectId);
}
