using OperationsService.Core.DTOs.Approvals;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Milestones;
using OperationsService.Core.DTOs.Projects;
using OperationsService.Core.DTOs.Resources;
using OperationsService.Core.DTOs.Tasks;

namespace OperationsService.Core.Interfaces.Services;

public interface IProjectService
{
    Task<ProjectReadDto?> GetByIdAsync(string id);
    Task<PaginatedResult<ProjectReadDto>> GetAllAsync(ProjectFilterParameters filters, string? departmentId);
    Task<ProjectReadDto> CreateAsync(CreateProjectDto dto, string managerId, string departmentId);
    Task<ProjectReadDto> UpdateAsync(string id, UpdateProjectDto dto, string userId);
    Task DeleteAsync(string id, string userId);
    Task<ProjectReadDto> SubmitForApprovalAsync(string id, string userId);
    Task<ProjectApprovalReadDto> ReviewMdApprovalAsync(string id, ReviewProjectApprovalDto dto, string reviewerId);
    Task<ProjectApprovalReadDto> ReviewFinanceApprovalAsync(string id, ReviewProjectApprovalDto dto, string reviewerId);
    Task<ProjectReadDto> ActivateAsync(string id, string userId);
    Task<ProjectReadDto> PutOnHoldAsync(string id, string reason, string userId);
    Task<ProjectReadDto> ResumeAsync(string id, string userId);
    Task<ProjectReadDto> CloseAsync(string id, string userId);

    // Milestones
    Task<MilestoneReadDto?> GetMilestoneByIdAsync(string milestoneId);
    Task<IEnumerable<MilestoneReadDto>> GetMilestonesAsync(string projectId);
    Task<MilestoneReadDto> CreateMilestoneAsync(string projectId, CreateMilestoneDto dto, string userId);
    Task<MilestoneReadDto> UpdateMilestoneAsync(string milestoneId, UpdateMilestoneDto dto, string userId);
    Task<MilestoneReadDto> SignOffMilestoneAsync(string milestoneId, SignOffMilestoneDto dto, string userId);
    Task<MilestoneUpdateLogReadDto> AddMilestoneUpdateAsync(string milestoneId, AddMilestoneUpdateDto dto, string userId);
    Task<IEnumerable<MilestoneUpdateLogReadDto>> GetMilestoneUpdatesAsync(string milestoneId);
    Task DeleteMilestoneAsync(string milestoneId, string userId);

    // Tasks
    Task<TaskReadDto?> GetTaskByIdAsync(string taskId);
    Task<IEnumerable<TaskReadDto>> GetTasksAsync(string milestoneId);
    Task<TaskReadDto> CreateTaskAsync(string milestoneId, CreateTaskDto dto, string userId);
    Task<TaskReadDto> UpdateTaskAsync(string taskId, UpdateTaskDto dto, string userId);
    Task<TaskReadDto> DispatchTaskAsync(string taskId, DispatchTaskDto dto, string userId, string departmentId);
    Task DeleteTaskAsync(string taskId, string userId);

    // Budget
    Task<BudgetSummaryDto> GetBudgetSummaryAsync(string projectId);
    Task<BudgetLineReadDto> CreateBudgetLineAsync(CreateBudgetLineDto dto, string userId);
    Task<BudgetLineReadDto> UpdateBudgetLineAsync(string lineId, UpdateBudgetLineDto dto, string userId);
    Task DeleteBudgetLineAsync(string lineId, string userId);
    Task<CostEntryReadDto> AddCostEntryAsync(CreateCostEntryDto dto, string userId);
    Task<IEnumerable<ProjectAlertLogReadDto>> GetProjectAlertsAsync(string projectId);

    // History (audit trail) + daily site reports
    Task<IEnumerable<ProjectHistoryReadDto>> GetProjectHistoryAsync(string projectId);
    Task<IEnumerable<ProjectDailyReportReadDto>> GetDailyReportsAsync(string projectId);
    Task<ProjectDailyReportReadDto> AddDailyReportAsync(string projectId, CreateProjectDailyReportDto dto, string userId);

    // Resources
    Task<IEnumerable<ProjectResourceReadDto>> GetResourcesAsync(string projectId);
    Task<ProjectResourceReadDto> AddResourceAsync(AddProjectResourceDto dto, string userId);
    Task<ProjectResourceReadDto> UpdateResourceAsync(string resourceId, UpdateProjectResourceDto dto, string userId);
    Task RemoveResourceAsync(string resourceId, string userId);
    /// <summary>PR1 — points the project at its signed contract in the attachment store.</summary>
    Task SetContractAttachmentAsync(string projectId, string attachmentId, string userId);
}
