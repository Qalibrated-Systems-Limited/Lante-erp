using TicketingService.Core.DTOs.Workflow;

namespace TicketingService.Core.Interfaces.Services;

public interface IWorkflowRuleService
{
    Task<IEnumerable<WorkflowRuleReadDto>> GetAllAsync();
    Task<WorkflowRuleReadDto?> GetByIdAsync(string id);
    Task<WorkflowRuleReadDto> CreateAsync(CreateWorkflowRuleDto dto, string createdByUserId);
    Task<WorkflowRuleReadDto> UpdateAsync(string id, UpdateWorkflowRuleDto dto, string updatedByUserId);
    Task<bool> DeleteAsync(string id);
    Task SetActiveAsync(string id, bool isActive, string updatedByUserId);
}
