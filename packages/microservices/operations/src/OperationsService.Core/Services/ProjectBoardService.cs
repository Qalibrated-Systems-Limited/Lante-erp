using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Board;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>PR4c — see <see cref="IProjectBoardService"/>.</summary>
public class ProjectBoardService(
    IGenericRepository<Project> projects,
    IGenericRepository<Milestone> milestones,
    IGenericRepository<ProjectTask> tasks) : IProjectBoardService
{
    /// <summary>Statuses that count as live work for the workload view.</summary>
    private static readonly ProjectStatus[] LiveProjects = [ProjectStatus.Active, ProjectStatus.OnHold];

    public async Task<ProjectBoardDto> GetBoardAsync(string projectId)
    {
        _ = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var today = DateTime.UtcNow.Date;

        var ms = await milestones.Query()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Order).ToListAsync();

        // One read for the whole project. The milestone-scoped endpoint would need a call per
        // milestone, and a board is exactly the case where you want them all at once.
        var ts = await tasks.Query()
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .ToListAsync();

        var titles = ms.ToDictionary(m => m.Id, m => m.Title);

        return new ProjectBoardDto
        {
            ProjectId = projectId,
            Milestones = ms.Select(m => new BoardMilestoneDto
            {
                Id = m.Id, Title = m.Title, Status = m.Status.ToString(),
                ProgressPct = m.ProgressPct, StartDate = m.StartDate, DueDate = m.DueDate,
                IsOverdue = m.DueDate.Date < today && m.Status != MilestoneStatus.Completed,
            }).ToList(),
            Tasks = ts.Select(t => ToDto(t, titles.GetValueOrDefault(t.MilestoneId), today))
                      .OrderBy(t => t.MilestoneTitle).ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                      .ToList(),
        };
    }

    public async Task<WorkloadDto> GetWorkloadAsync(DateTime? from, DateTime? to, string? departmentId = null)
    {
        var start = (from ?? DateTime.UtcNow.Date).Date;
        var end   = (to ?? start.AddDays(27)).Date;
        if (end < start) (start, end) = (end, start);

        var live = await projects.Query()
            .Where(p => !p.IsDeleted && LiveProjects.Contains(p.Status)
                     && (departmentId == null || p.DepartmentId == departmentId))
            .ToListAsync();
        if (live.Count == 0)
            return new WorkloadDto { From = start, To = end, WorkingDays = WorkingDaysBetween(start, end) };

        var ids = live.Select(p => p.Id).ToList();
        var names = live.ToDictionary(p => p.Id, p => p.Name);

        var all = await tasks.Query()
            .Where(t => ids.Contains(t.ProjectId) && !t.IsDeleted && t.Status != Enums.TaskStatus.Done)
            .ToListAsync();

        // A task counts against the window if any part of it falls inside. Using only the due date
        // would hide a long task that started weeks ago and is still consuming someone's time.
        var inWindow = all.Where(t => Overlaps(t, start, end)).ToList();
        var today = DateTime.UtcNow.Date;

        var rows = inWindow
            .Where(t => !string.IsNullOrWhiteSpace(t.AssignedToUserId))
            .GroupBy(t => t.AssignedToUserId!)
            .Select(g => new WorkloadRowDto
            {
                UserId = g.Key,
                TaskCount = g.Count(),
                EstimatedHours = g.Sum(t => t.EstimatedHours ?? 0m),
                UnestimatedCount = g.Count(t => t.EstimatedHours is null or 0m),
                OverdueCount = g.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date < today),
                ProjectCount = g.Select(t => t.ProjectId).Distinct().Count(),
                ProjectNames = g.Select(t => names.GetValueOrDefault(t.ProjectId, "?")).Distinct().OrderBy(n => n).ToList(),
            })
            // Busiest first by hours, then by task count so people with unestimated work still surface.
            .OrderByDescending(r => r.EstimatedHours).ThenByDescending(r => r.TaskCount)
            .ToList();

        var unassigned = inWindow.Where(t => string.IsNullOrWhiteSpace(t.AssignedToUserId)).ToList();

        return new WorkloadDto
        {
            From = start, To = end,
            WorkingDays = WorkingDaysBetween(start, end),
            Rows = rows,
            UnassignedCount = unassigned.Count,
            UnassignedHours = unassigned.Sum(t => t.EstimatedHours ?? 0m),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Does the task touch the window at all? A task with neither date is treated as current — it is
    /// open work someone is carrying, and dropping it would make the view flatter than reality.
    /// </summary>
    private static bool Overlaps(ProjectTask t, DateTime start, DateTime end)
    {
        var s = t.StartDate?.Date;
        var e = t.DueDate?.Date;
        if (s is null && e is null) return true;
        return (s ?? e!.Value) <= end && (e ?? s!.Value) >= start;
    }

    /// <summary>
    /// Mon–Fri count. Deliberately not the HR work calendar: that lives in another service and this is
    /// a yardstick for reading a bar, not a payroll figure.
    /// </summary>
    private static int WorkingDaysBetween(DateTime start, DateTime end)
    {
        var days = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
        return days;
    }

    private static BoardTaskDto ToDto(ProjectTask t, string? milestoneTitle, DateTime today) => new()
    {
        Id = t.Id, MilestoneId = t.MilestoneId, MilestoneTitle = milestoneTitle,
        Title = t.Title, Description = t.Description,
        Status = t.Status.ToString(), AssignedToUserId = t.AssignedToUserId,
        StartDate = t.StartDate, DueDate = t.DueDate, EstimatedHours = t.EstimatedHours,
        ParentTaskId = t.ParentTaskId,
        IsOverdue = t.DueDate.HasValue && t.DueDate.Value.Date < today && t.Status != Enums.TaskStatus.Done,
    };
}
