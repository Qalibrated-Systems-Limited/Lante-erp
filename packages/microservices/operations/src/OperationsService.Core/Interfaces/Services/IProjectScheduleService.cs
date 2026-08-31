using OperationsService.Core.DTOs.Schedule;
using OperationsService.Core.Entities;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>PR1 — project baselines, finish-to-start dependencies and the critical path.</summary>
public interface IProjectScheduleService
{
    /// <summary>Freezes milestone dates and the budget as the baseline. Returns milestones captured.</summary>
    Task<int> SetBaselineAsync(string projectId, string userId, bool force = false);

    Task<MilestoneDependency> LinkMilestonesAsync(string projectId, string predecessorId, string successorId, int lagDays, string userId);
    Task<TaskDependency>      LinkTasksAsync(string projectId, string predecessorId, string successorId, int lagDays, string userId);
    Task UnlinkMilestonesAsync(string dependencyId);
    Task UnlinkTasksAsync(string dependencyId);

    /// <summary>Task-level links for the whole project. Separate from the milestone schedule so the
    /// Gantt does not have to carry every task on a large project just to show a few links.</summary>
    Task<List<DependencyDto>> GetTaskDependenciesAsync(string projectId);

    Task<ProjectScheduleDto> GetScheduleAsync(string projectId);
}
