using OperationsService.Core.DTOs.Board;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// PR4c — board, calendar and workload projections. Read-only: these views re-present the milestones
/// and tasks PR1 made complete, and store nothing of their own.
/// </summary>
public interface IProjectBoardService
{
    /// <summary>Every milestone and task on a project in one read — what a board or calendar needs.</summary>
    Task<ProjectBoardDto> GetBoardAsync(string projectId);

    /// <summary>Open work per person across live projects, for a date window.</summary>
    Task<WorkloadDto> GetWorkloadAsync(DateTime? from, DateTime? to, string? departmentId = null);
}
