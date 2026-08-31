using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Schedule;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>
/// PR1 — the project schedule: baselines, finish-to-start dependencies, and the critical path.
///
/// Dependencies are finish-to-start only and the graph must stay acyclic; both rules are enforced
/// here rather than in the database, because the useful error is "adding this would create a cycle
/// A → B → A", which a unique index cannot express.
/// </summary>
public class ProjectScheduleService(
    IGenericRepository<Project> projects,
    IGenericRepository<Milestone> milestones,
    IGenericRepository<ProjectTask> tasks,
    IGenericRepository<MilestoneDependency> milestoneDeps,
    IGenericRepository<TaskDependency> taskDeps,
    IGenericRepository<ProjectHistory> history) : IProjectScheduleService
{
    // ── Baseline ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Freezes the current plan as the baseline: milestone dates and the approved budget total.
    /// Called on activation. Re-baselining an already-baselined project is refused — that is what an
    /// approved change request is for (PR3), and silently overwriting a baseline destroys the only
    /// record of what was agreed.
    /// </summary>
    public async Task<int> SetBaselineAsync(string projectId, string userId, bool force = false)
    {
        var project = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        if (project.BaselineSetAt != null && !force)
            throw new InvalidOperationException(
                $"Project '{project.Name}' was already baselined on {project.BaselineSetAt:yyyy-MM-dd}. " +
                "Raise an approved change request to move the baseline.");

        var ms = await milestones.Query().Where(m => m.ProjectId == projectId).ToListAsync();
        foreach (var m in ms)
        {
            m.BaselineStart = m.StartDate;
            m.BaselineDue   = m.DueDate;
            await milestones.UpdateAsync(m);
        }

        project.BaselineBudget = project.PlannedBudget;
        project.BaselineSetAt  = DateTime.UtcNow;
        project.UpdatedAt      = DateTime.UtcNow;
        project.UpdatedBy      = userId;
        await projects.UpdateAsync(project);

        await history.CreateAsync(new ProjectHistory
        {
            ProjectId = projectId,
            Action    = "BaselineSet",
            Notes     = $"Baseline captured: {ms.Count} milestone(s), budget {project.PlannedBudget:N2}.",
            ChangedByUserId = userId, CreatedBy = userId, UpdatedBy = userId,
        });

        return ms.Count;
    }

    // ── Dependencies ─────────────────────────────────────────────────────────

    public async Task<MilestoneDependency> LinkMilestonesAsync(
        string projectId, string predecessorId, string successorId, int lagDays, string userId)
    {
        if (predecessorId == successorId)
            throw new InvalidOperationException("A milestone cannot depend on itself.");
        if (lagDays < 0)
            throw new InvalidOperationException(
                "Lag cannot be negative — a finish-to-start link means the successor starts after the predecessor finishes.");

        var ms = await milestones.Query().Where(m => m.ProjectId == projectId).ToListAsync();
        var byId = ms.ToDictionary(m => m.Id);
        if (!byId.ContainsKey(predecessorId)) throw new KeyNotFoundException("Predecessor milestone not found on this project.");
        if (!byId.ContainsKey(successorId))   throw new KeyNotFoundException("Successor milestone not found on this project.");

        var existing = await milestoneDeps.Query().Where(d => d.ProjectId == projectId).ToListAsync();
        if (existing.Any(d => d.PredecessorMilestoneId == predecessorId && d.SuccessorMilestoneId == successorId))
            throw new InvalidOperationException("That dependency already exists.");

        var edges = existing.ToDictionary(d => d.Id, d => (d.PredecessorMilestoneId, d.SuccessorMilestoneId));
        if (WouldCycle(edges.Values, predecessorId, successorId, out var path))
            throw new InvalidOperationException(
                $"That link would create a circular dependency: {string.Join(" → ", path.Select(id => byId[id].Title))}.");

        var dep = await milestoneDeps.CreateAsync(new MilestoneDependency
        {
            ProjectId = projectId,
            PredecessorMilestoneId = predecessorId,
            SuccessorMilestoneId   = successorId,
            LagDays   = lagDays,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await history.CreateAsync(new ProjectHistory
        {
            ProjectId = projectId, Action = "MilestoneLinked",
            Notes     = $"'{byId[successorId].Title}' now starts after '{byId[predecessorId].Title}'" +
                        (lagDays > 0 ? $" + {lagDays}d lag." : "."),
            ChangedByUserId = userId, CreatedBy = userId, UpdatedBy = userId,
        });
        return dep;
    }

    public async Task<TaskDependency> LinkTasksAsync(
        string projectId, string predecessorId, string successorId, int lagDays, string userId)
    {
        if (predecessorId == successorId)
            throw new InvalidOperationException("A task cannot depend on itself.");
        if (lagDays < 0)
            throw new InvalidOperationException("Lag cannot be negative on a finish-to-start link.");

        var ts = await tasks.Query().Where(t => t.ProjectId == projectId).ToListAsync();
        var byId = ts.ToDictionary(t => t.Id);
        if (!byId.ContainsKey(predecessorId)) throw new KeyNotFoundException("Predecessor task not found on this project.");
        if (!byId.ContainsKey(successorId))   throw new KeyNotFoundException("Successor task not found on this project.");

        var existing = await taskDeps.Query().Where(d => d.ProjectId == projectId).ToListAsync();
        if (existing.Any(d => d.PredecessorTaskId == predecessorId && d.SuccessorTaskId == successorId))
            throw new InvalidOperationException("That dependency already exists.");

        var edges = existing.Select(d => (d.PredecessorTaskId, d.SuccessorTaskId));
        if (WouldCycle(edges, predecessorId, successorId, out var path))
            throw new InvalidOperationException(
                $"That link would create a circular dependency: {string.Join(" → ", path.Select(id => byId[id].Title))}.");

        var dep = await taskDeps.CreateAsync(new TaskDependency
        {
            ProjectId = projectId,
            PredecessorTaskId = predecessorId,
            SuccessorTaskId   = successorId,
            LagDays   = lagDays,
            CreatedBy = userId, UpdatedBy = userId,
        });
        return dep;
    }

    public async Task UnlinkMilestonesAsync(string dependencyId)
    {
        var dep = await milestoneDeps.GetByIdAsync(dependencyId)
            ?? throw new KeyNotFoundException("Dependency not found.");
        await milestoneDeps.DeleteAsync(dep);
    }

    public async Task UnlinkTasksAsync(string dependencyId)
    {
        var dep = await taskDeps.GetByIdAsync(dependencyId)
            ?? throw new KeyNotFoundException("Dependency not found.");
        await taskDeps.DeleteAsync(dep);
    }

    /// <summary>
    /// True if adding predecessor → successor would close a loop. Walks forward from the proposed
    /// successor; if it can reach the predecessor, the new edge completes a cycle. Returns the
    /// offending path so the error can name it rather than just refusing.
    /// </summary>
    private static bool WouldCycle(
        IEnumerable<(string Pred, string Succ)> edges, string predecessorId, string successorId,
        out List<string> path)
    {
        var forward = edges
            .GroupBy(e => e.Pred)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Succ).ToList());

        var stack = new Stack<(string Node, List<string> Trail)>();
        stack.Push((successorId, [successorId]));
        var seen = new HashSet<string>();

        while (stack.Count > 0)
        {
            var (node, trail) = stack.Pop();
            if (node == predecessorId) { trail.Add(successorId); path = trail; return true; }
            if (!seen.Add(node)) continue;
            if (!forward.TryGetValue(node, out var next)) continue;
            foreach (var n in next) stack.Push((n, [.. trail, n]));
        }

        path = [];
        return false;
    }

    public async Task<List<DependencyDto>> GetTaskDependenciesAsync(string projectId)
    {
        var deps = await taskDeps.Query().Where(d => d.ProjectId == projectId).ToListAsync();
        return deps.Select(d => new DependencyDto
        {
            Id = d.Id, PredecessorId = d.PredecessorTaskId,
            SuccessorId = d.SuccessorTaskId, LagDays = d.LagDays,
        }).ToList();
    }

    // ── Schedule read model ──────────────────────────────────────────────────

    /// <summary>
    /// The project schedule: every milestone with its dates, baseline variance, dependencies and
    /// whether it sits on the critical path.
    /// </summary>
    public async Task<ProjectScheduleDto> GetScheduleAsync(string projectId)
    {
        var project = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var ms   = await milestones.Query().Where(m => m.ProjectId == projectId).OrderBy(m => m.Order).ToListAsync();
        var deps = await milestoneDeps.Query().Where(d => d.ProjectId == projectId).ToListAsync();

        var critical = ComputeCriticalPath(ms, deps);

        var items = ms.Select(m => new ScheduleItemDto
        {
            Id        = m.Id,
            Title     = m.Title,
            Order     = m.Order,
            Status    = m.Status.ToString(),
            StartDate = m.StartDate,
            DueDate   = m.DueDate,
            BaselineStart = m.BaselineStart,
            BaselineDue   = m.BaselineDue,
            ProgressPct   = m.ProgressPct,
            SignOffAt     = m.SignOffAt,
            ScheduleVarianceDays = m.ScheduleVarianceDays,
            IsCritical    = critical.Contains(m.Id),
            PredecessorIds = deps.Where(d => d.SuccessorMilestoneId == m.Id)
                                 .Select(d => d.PredecessorMilestoneId).ToList(),
        }).ToList();

        return new ProjectScheduleDto
        {
            ProjectId     = projectId,
            ProjectName   = project.Name,
            BaselineSetAt = project.BaselineSetAt,
            PlannedStart  = ms.Where(m => m.StartDate != null).Select(m => m.StartDate!.Value).DefaultIfEmpty(project.StartDate).Min(),
            PlannedFinish = ms.Select(m => m.DueDate).DefaultIfEmpty(project.ExpectedEndDate).Max(),
            // The project is late by the worst late milestone, not the average — an average would let
            // several early milestones disguise one badly overdue one.
            WorstVarianceDays = items.Where(i => i.ScheduleVarianceDays.HasValue)
                                     .Select(i => i.ScheduleVarianceDays!.Value)
                                     .DefaultIfEmpty(0).Max(),
            Dependencies = deps.Select(d => new DependencyDto
            {
                Id = d.Id, PredecessorId = d.PredecessorMilestoneId,
                SuccessorId = d.SuccessorMilestoneId, LagDays = d.LagDays,
            }).ToList(),
            Items = items,
        };
    }

    /// <summary>
    /// Longest chain through the dependency graph, measured in days. Milestones on it have no float:
    /// slipping one slips the project. Falls back to an empty set when nothing is linked — with no
    /// dependencies every milestone is independent, so "critical" would be meaningless rather than
    /// "everything".
    /// </summary>
    private static HashSet<string> ComputeCriticalPath(List<Milestone> ms, List<MilestoneDependency> deps)
    {
        if (deps.Count == 0 || ms.Count == 0) return [];

        var byId = ms.ToDictionary(m => m.Id);
        var predecessors = deps.GroupBy(d => d.SuccessorMilestoneId)
                               .ToDictionary(g => g.Key, g => g.ToList());

        static int Duration(Milestone m) =>
            m.StartDate is null ? 1 : Math.Max(1, (int)(m.DueDate.Date - m.StartDate.Value.Date).TotalDays);

        // Longest path ending at each node, plus the predecessor that produced it.
        var longest = new Dictionary<string, int>();
        var cameFrom = new Dictionary<string, string?>();
        var visiting = new HashSet<string>();

        int Solve(string id)
        {
            if (longest.TryGetValue(id, out var cached)) return cached;
            // Cycles are prevented on insert; guard anyway so a bad row cannot hang the request.
            if (!visiting.Add(id)) return 0;

            var best = 0; string? bestFrom = null;
            if (predecessors.TryGetValue(id, out var preds))
            {
                foreach (var p in preds)
                {
                    if (!byId.ContainsKey(p.PredecessorMilestoneId)) continue;
                    var candidate = Solve(p.PredecessorMilestoneId) + p.LagDays;
                    if (candidate > best) { best = candidate; bestFrom = p.PredecessorMilestoneId; }
                }
            }

            visiting.Remove(id);
            cameFrom[id] = bestFrom;
            var total = best + Duration(byId[id]);
            longest[id] = total;
            return total;
        }

        foreach (var m in ms) Solve(m.Id);

        var end = longest.OrderByDescending(kv => kv.Value).First().Key;
        var path = new HashSet<string>();
        for (string? at = end; at != null; at = cameFrom.GetValueOrDefault(at)) path.Add(at);
        return path;
    }
}
