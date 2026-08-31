using FinanceService.Core.DTOs;

namespace FinanceService.Core.Interfaces;

public interface IBudgetService
{
    Task<BudgetReadDto> CreateBudgetAsync(CreateBudgetDto dto, string? actor);
    Task<List<BudgetReadDto>> ListBudgetsAsync(string fiscalYearId, bool includeSuperseded = false);
    Task<RevenueTargetReadDto> CreateTargetAsync(CreateRevenueTargetDto dto, string? actor);
    Task<List<RevenueTargetReadDto>> ListTargetsAsync(string fiscalYearId);
}
