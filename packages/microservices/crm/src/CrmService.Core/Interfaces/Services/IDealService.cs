using CrmService.Core.DTOs.Deals;

namespace CrmService.Core.Interfaces.Services;

public interface IDealService
{
    Task<DealListResult> GetAllAsync(DealFilterParams filter);
    Task<DealDetailDto?> GetByIdAsync(string id);
    Task<DealDetailDto> CreateFromOpportunityAsync(CreateDealDto dto, string userId);
    Task<DealDetailDto> UpdateAsync(string id, UpdateDealDto dto, string userId);
    Task<ContractDto> RegisterContractAsync(string id, RegisterContractDto dto, string userId);
    Task<DealActionResult> CreateProjectAsync(string id, string userId);
    Task<DealActionResult> CloseAsync(string id, string userId);
}
