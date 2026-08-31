using CrmService.Core.DTOs.Opportunities;

namespace CrmService.Core.Interfaces.Services;

public interface IOpportunityService
{
    Task<List<PipelineStageDto>> GetStagesAsync();
    Task<PipelineBoardResult> GetBoardAsync(string? assignedTo);
    Task<OpportunityListResult> GetAllAsync(OpportunityFilterParams filter);
    Task<OpportunityDetailDto?> GetByIdAsync(string id);
    Task<OpportunityDetailDto> CreateAsync(CreateOpportunityDto dto, string userId, string? userName);
    Task<OpportunityDetailDto> UpdateAsync(string id, UpdateOpportunityDto dto, string userId);
    Task<OpportunityActionResult> AdvanceStageAsync(string id, AdvanceStageDto dto, string userId);
    Task<OpportunityActionResult> MarkWonAsync(string id, string userId);
    Task<OpportunityActionResult> MarkLostAsync(string id, MarkLostDto dto, string userId);
    Task<OpportunityActivityDto> AddActivityAsync(string id, CreateOpportunityActivityDto dto, string userId);
}
