using OperationsService.Core.DTOs.Budget;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>PR1 — detailed approvable budgets, the contract rate card, and quote-vs-spend.</summary>
public interface IProjectBudgetService
{
    Task<List<BudgetVersionDto>> GetVersionsAsync(string projectId);
    Task<BudgetVersionDto?>      GetVersionAsync(string versionId);
    Task<BudgetVersionDto>       CreateVersionAsync(string projectId, CreateBudgetVersionDto dto, string userId);

    Task<BudgetVersionDto> UpsertLineAsync(string versionId, UpsertBudgetLineDto dto, string userId);
    Task<BudgetVersionDto> DeleteLineAsync(string versionId, string lineId, string userId);

    Task<BudgetVersionDto> SubmitAsync(string versionId, string userId);
    Task<BudgetVersionDto> ApproveAsync(string versionId, string userId);
    Task<BudgetVersionDto> RejectAsync(string versionId, string reason, string userId);

    Task<List<ContractRateDto>> GetRatesAsync(string projectId, bool activeOnly = false);
    Task<ContractRateDto>       AddRateAsync(string projectId, UpsertContractRateDto dto, string userId);
    Task DeactivateRateAsync(string rateId, string userId);

    Task<ProjectCommercialsDto> GetCommercialsAsync(string projectId);
}
