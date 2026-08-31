using OperationsService.Core.DTOs.Templates;
using OperationsService.Core.Entities;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// PR4b — project templates: the milestones, tasks and priced budget lines a standard job always has,
/// and the act of turning one into a real project.
/// </summary>
public interface IProjectTemplateService
{
    Task<List<ProjectTemplateDto>> GetAllAsync(bool includeInactive = false);
    Task<ProjectTemplateDetailDto?> GetByIdAsync(string templateId);
    Task<ProjectTemplateDetailDto> SaveAsync(string? templateId, SaveProjectTemplateDto dto, string userId);
    /// <summary>Deactivates rather than removes — projects raised from it still point back here.</summary>
    Task DeleteAsync(string templateId, string userId);

    /// <summary>Raises a new DRAFT project from the template. Returns the new project id.</summary>
    Task<string> InstantiateAsync(string templateId, InstantiateTemplateDto dto, string userId);

    /// <summary>
    /// Writes a template's milestones, tasks and draft budget onto an existing project. Shared with
    /// the recurrence sweep so generated and hand-raised projects come out identical.
    /// </summary>
    Task InstantiateInto(Project project, ProjectTemplateDetailDto detail, DateTime start, decimal contractValue, string userId);
}

/// <summary>PR4b — standing schedules that raise a project from a template on a cycle.</summary>
public interface IRecurringProjectService
{
    Task<List<RecurringScheduleDto>> GetAllAsync(bool includeInactive = false);
    Task<RecurringScheduleDto> SaveAsync(string? scheduleId, SaveRecurringScheduleDto dto, string userId);
    Task<RecurringScheduleDto> SetActiveAsync(string scheduleId, bool active, string userId);

    /// <summary>Raises the next occurrence now, ahead of its lead-time window. Returns the project id.</summary>
    Task<string> RunNowAsync(string scheduleId, string userId);

    /// <summary>
    /// Raises every occurrence whose lead-time window has opened. Called by the background sweep;
    /// returns how many projects it created.
    /// </summary>
    Task<int> GenerateDueAsync(string userId, CancellationToken ct = default);
}
