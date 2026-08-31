using CrmService.Core.DTOs.Activity;

namespace CrmService.Core.Interfaces.Services;

public interface IActivityService
{
    // Interactions
    Task<CustomerInteractionDto> LogInteractionAsync(string customerId, LogInteractionDto dto, string userId);
    Task<List<CustomerInteractionDto>> GetInteractionsAsync(string customerId);

    // Tasks
    Task<TaskListResult> GetTasksAsync(TaskFilterParams filter);
    Task<ActivityTaskDto> CreateTaskAsync(CreateTaskDto dto, string userId);
    Task<ActivityTaskDto> CompleteTaskAsync(string id, string userId);
    Task<ActivityTaskDto> CancelTaskAsync(string id, string userId);

    // Visits
    Task<ClientVisitDto> LogVisitAsync(LogVisitDto dto, string userId);
    Task<List<ClientVisitDto>> GetVisitsAsync(string? customerId, string? employeeId);

    // Visit targets
    Task<List<VisitTargetDto>> GetVisitTargetsAsync();
    Task<VisitTargetDto> SaveVisitTargetAsync(SaveVisitTargetDto dto, string userId);

    // Daily activity log
    Task<SalesActivityLogDto> LogDailyActivityAsync(LogDailyActivityDto dto, string userId);
    Task<List<SalesActivityLogDto>> GetDailyActivityAsync(string employeeId, DateTime? from, DateTime? to);
}
