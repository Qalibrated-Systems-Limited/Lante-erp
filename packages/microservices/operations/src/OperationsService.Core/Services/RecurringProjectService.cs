using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Templates;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>PR4b — see <see cref="IRecurringProjectService"/>.</summary>
public class RecurringProjectService(
    IGenericRepository<RecurringProjectSchedule> schedules,
    IGenericRepository<ProjectTemplate> templates,
    IGenericRepository<Project> projects,
    IProjectTemplateService templateService) : IRecurringProjectService
{
    public async Task<List<RecurringScheduleDto>> GetAllAsync(bool includeInactive = false)
    {
        var q = schedules.Query().Where(s => !s.IsDeleted);
        if (!includeInactive) q = q.Where(s => s.IsActive);

        var list = await q.OrderBy(s => s.NextDueDate).ToListAsync();
        var ids = list.Select(s => s.TemplateId).Distinct().ToList();
        var names = await templates.Query().Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        return list.Select(s => Map(s, names.GetValueOrDefault(s.TemplateId))).ToList();
    }

    public async Task<RecurringScheduleDto> SaveAsync(string? scheduleId, SaveRecurringScheduleDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("A schedule needs a name.");

        var template = await templates.GetByIdAsync(dto.TemplateId)
            ?? throw new KeyNotFoundException("The template for this schedule was not found.");
        if (!template.IsActive)
            throw new InvalidOperationException($"Template '{template.Name}' is inactive and cannot be scheduled.");

        RecurringProjectSchedule s;
        if (string.IsNullOrWhiteSpace(scheduleId))
        {
            s = new RecurringProjectSchedule { CreatedBy = userId };
        }
        else
        {
            s = await schedules.GetByIdAsync(scheduleId)
                ?? throw new KeyNotFoundException($"Schedule {scheduleId} not found.");
        }

        s.TemplateId       = dto.TemplateId;
        s.Name             = dto.Name.Trim();
        s.Description      = dto.Description;
        s.ClientName       = dto.ClientName;
        s.ClientId         = dto.ClientId;
        s.DepartmentId     = dto.DepartmentId ?? s.DepartmentId;
        s.ProjectManagerId = dto.ProjectManagerId ?? (string.IsNullOrEmpty(s.ProjectManagerId) ? userId : s.ProjectManagerId);
        s.ContractValue    = dto.ContractValue;
        s.Frequency        = ParseEnum(dto.Frequency, s.Frequency);
        s.Interval         = Math.Max(1, dto.Interval ?? s.Interval);
        s.LeadTimeDays     = Math.Max(0, dto.LeadTimeDays ?? s.LeadTimeDays);
        s.NextDueDate      = (dto.NextDueDate ?? (s.NextDueDate == default ? DateTime.UtcNow.Date : s.NextDueDate)).Date;
        s.EndDate          = dto.EndDate?.Date;
        if (dto.IsActive.HasValue) s.IsActive = dto.IsActive.Value;
        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.UtcNow;

        if (s.EndDate.HasValue && s.EndDate.Value < s.NextDueDate)
            throw new InvalidOperationException("The end date is before the next occurrence is due.");

        s = string.IsNullOrWhiteSpace(scheduleId)
            ? await schedules.CreateAsync(s)
            : await schedules.UpdateAsync(s);

        return Map(s, template.Name);
    }

    public async Task<RecurringScheduleDto> SetActiveAsync(string scheduleId, bool active, string userId)
    {
        var s = await schedules.GetByIdAsync(scheduleId)
            ?? throw new KeyNotFoundException($"Schedule {scheduleId} not found.");
        s.IsActive  = active;
        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.UtcNow;
        await schedules.UpdateAsync(s);

        var t = await templates.GetByIdAsync(s.TemplateId);
        return Map(s, t?.Name);
    }

    public async Task<string> RunNowAsync(string scheduleId, string userId)
    {
        var s = await schedules.GetByIdAsync(scheduleId)
            ?? throw new KeyNotFoundException($"Schedule {scheduleId} not found.");
        return await GenerateOneAsync(s, userId)
            ?? throw new InvalidOperationException("Could not raise a project — check the template is still active.");
    }

    public async Task<int> GenerateDueAsync(string userId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var due = await schedules.Query()
            .Where(s => s.IsActive && !s.IsDeleted
                     && (s.EndDate == null || s.EndDate >= today))
            .ToListAsync(ct);

        var created = 0;
        foreach (var s in due)
        {
            // The lead-time window is what makes this useful: raise the project before the visit is
            // due, so there is time to plan it rather than discovering it on the day.
            if (s.NextDueDate.Date.AddDays(-s.LeadTimeDays) > today) continue;

            if (await GenerateOneAsync(s, userId) is not null) created++;
        }
        return created;
    }

    /// <summary>
    /// Raises one occurrence and rolls the schedule forward. Advances past any windows already missed
    /// — if the service was down for two cycles, the next due date should be the next real one, not a
    /// date in the past that would fire again on every sweep.
    /// </summary>
    private async Task<string?> GenerateOneAsync(RecurringProjectSchedule s, string userId)
    {
        var detail = await templateService.GetByIdAsync(s.TemplateId);
        if (detail is null) return null;

        var start = s.NextDueDate.Date;
        var template = await templates.GetByIdAsync(s.TemplateId);

        var project = await projects.CreateAsync(new Project
        {
            // The date is in the name deliberately — a client with an annual recalibration ends up
            // with a row per year, and "Bidco recalibration" three times over is unusable.
            Name = $"{s.Name} — {start:MMM yyyy}",
            ClientName = s.ClientName,
            ClientId   = s.ClientId,
            Type = template?.Type ?? ProjectType.Calibration,
            Status = ProjectStatus.Draft,
            DepartmentId = s.DepartmentId,
            ProjectManagerId = s.ProjectManagerId,
            ContractValue = s.ContractValue,
            StartDate = start,
            ExpectedEndDate = start.AddDays(
                detail.Milestones.Count == 0 ? 30 : detail.Milestones.Max(m => m.OffsetDays + m.DurationDays)),
            ScopeSummary = s.Description ?? template?.Description,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await templateService.InstantiateInto(project, detail, start, s.ContractValue, userId);

        s.LastGeneratedAt        = DateTime.UtcNow;
        s.LastGeneratedProjectId = project.Id;
        s.GeneratedCount        += 1;
        s.NextDueDate            = NextAfter(s, s.NextDueDate);
        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.UtcNow;
        await schedules.UpdateAsync(s);

        return project.Id;
    }

    /// <summary>
    /// The next due date strictly after <paramref name="from"/>, skipping any cycles already in the
    /// past. Month arithmetic, so a quarterly visit keeps its day of the month rather than drifting.
    /// </summary>
    private static DateTime NextAfter(RecurringProjectSchedule s, DateTime from)
    {
        var months = s.Frequency switch
        {
            RecurrenceFrequency.Monthly      => 1,
            RecurrenceFrequency.Quarterly    => 3,
            RecurrenceFrequency.SemiAnnually => 6,
            _                                => 12,
        } * Math.Max(1, s.Interval);

        var today = DateTime.UtcNow.Date;
        var next = from.AddMonths(months);
        // Guard the loop as well as the condition: a corrupt zero-month cycle would otherwise spin.
        var guard = 0;
        while (next <= today && guard++ < 240) next = next.AddMonths(months);
        return next;
    }

    private static RecurringScheduleDto Map(RecurringProjectSchedule s, string? templateName) => new()
    {
        Id = s.Id, TemplateId = s.TemplateId, TemplateName = templateName,
        Name = s.Name, Description = s.Description,
        ClientName = s.ClientName, ClientId = s.ClientId,
        DepartmentId = s.DepartmentId, ProjectManagerId = s.ProjectManagerId,
        ContractValue = s.ContractValue,
        Frequency = s.Frequency.ToString(), Interval = s.Interval, LeadTimeDays = s.LeadTimeDays,
        NextDueDate = s.NextDueDate, EndDate = s.EndDate, IsActive = s.IsActive,
        LastGeneratedAt = s.LastGeneratedAt, LastGeneratedProjectId = s.LastGeneratedProjectId,
        GeneratedCount = s.GeneratedCount,
        DueNow = s.IsActive && s.NextDueDate.Date.AddDays(-s.LeadTimeDays) <= DateTime.UtcNow.Date,
    };

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
}
