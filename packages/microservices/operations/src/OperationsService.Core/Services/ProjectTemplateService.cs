using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Templates;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>PR4b — see <see cref="IProjectTemplateService"/>.</summary>
public class ProjectTemplateService(
    IGenericRepository<ProjectTemplate> templates,
    IGenericRepository<ProjectTemplateMilestone> templateMilestones,
    IGenericRepository<ProjectTemplateTask> templateTasks,
    IGenericRepository<ProjectTemplateBudgetLine> templateLines,
    IGenericRepository<Project> projects,
    IGenericRepository<Milestone> milestones,
    IGenericRepository<ProjectTask> tasks,
    IGenericRepository<BudgetVersion> budgetVersions,
    IGenericRepository<BudgetLine> budgetLines,
    IGenericRepository<ProjectHistory> history) : IProjectTemplateService
{
    // ── Templates ────────────────────────────────────────────────────────────────

    public async Task<List<ProjectTemplateDto>> GetAllAsync(bool includeInactive = false)
    {
        var q = templates.Query().Where(t => !t.IsDeleted);
        if (!includeInactive) q = q.Where(t => t.IsActive);

        var list = await q.OrderByDescending(t => t.UseCount).ThenBy(t => t.Name).ToListAsync();
        var ids = list.Select(t => t.Id).ToList();

        var ms = await templateMilestones.Query().Where(m => ids.Contains(m.TemplateId) && !m.IsDeleted).ToListAsync();
        var lines = await templateLines.Query().Where(l => ids.Contains(l.TemplateId) && !l.IsDeleted).ToListAsync();

        return list.Select(t => new ProjectTemplateDto
        {
            Id = t.Id, Name = t.Name, Description = t.Description,
            Type = t.Type.ToString(), DepartmentId = t.DepartmentId,
            IsActive = t.IsActive, UseCount = t.UseCount, CreatedAt = t.CreatedAt,
            MilestoneCount  = ms.Count(m => m.TemplateId == t.Id),
            BudgetLineCount = lines.Count(l => l.TemplateId == t.Id),
        }).ToList();
    }

    public async Task<ProjectTemplateDetailDto?> GetByIdAsync(string templateId)
    {
        var t = await templates.GetByIdAsync(templateId);
        if (t is null || t.IsDeleted) return null;

        var ms = await templateMilestones.Query()
            .Where(m => m.TemplateId == templateId && !m.IsDeleted).OrderBy(m => m.Order).ToListAsync();
        var msIds = ms.Select(m => m.Id).ToList();
        var ts = await templateTasks.Query()
            .Where(x => msIds.Contains(x.TemplateMilestoneId) && !x.IsDeleted).OrderBy(x => x.Order).ToListAsync();
        var lines = await templateLines.Query()
            .Where(l => l.TemplateId == templateId && !l.IsDeleted).ToListAsync();

        return new ProjectTemplateDetailDto
        {
            Id = t.Id, Name = t.Name, Description = t.Description,
            Type = t.Type.ToString(), DepartmentId = t.DepartmentId,
            IsActive = t.IsActive, UseCount = t.UseCount, CreatedAt = t.CreatedAt,
            MilestoneCount = ms.Count, BudgetLineCount = lines.Count,
            TotalValuePct = ms.Sum(m => m.ValuePct),
            Milestones = ms.Select(m => new TemplateMilestoneDto
            {
                Id = m.Id, Title = m.Title, Description = m.Description, Order = m.Order,
                OffsetDays = m.OffsetDays, DurationDays = m.DurationDays,
                ValuePct = m.ValuePct, IsBillable = m.IsBillable,
                Tasks = ts.Where(x => x.TemplateMilestoneId == m.Id).Select(x => new TemplateTaskDto
                {
                    Id = x.Id, Title = x.Title, Description = x.Description,
                    Order = x.Order, EstimatedHours = x.EstimatedHours,
                }).ToList(),
            }).ToList(),
            BudgetLines = lines.Select(l => new TemplateBudgetLineDto
            {
                Id = l.Id, Category = l.Category.ToString(), Description = l.Description,
                RateCode = l.RateCode, Quantity = l.Quantity,
                UnitCostRate = l.UnitCostRate, UnitClientRate = l.UnitClientRate,
            }).ToList(),
        };
    }

    public async Task<ProjectTemplateDetailDto> SaveAsync(string? templateId, SaveProjectTemplateDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("A template needs a name.");

        // Value percentages are how the contract value gets split across milestones. Letting them
        // exceed 100 would generate a project whose milestones are worth more than the job.
        var totalPct = (dto.Milestones ?? []).Sum(m => m.ValuePct);
        if (totalPct > 100.01m)
            throw new InvalidOperationException($"Milestone values total {totalPct:0.#}% — they cannot exceed 100% of the contract.");

        ProjectTemplate template;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            template = await templates.CreateAsync(new ProjectTemplate
            {
                Name = dto.Name.Trim(), Description = dto.Description,
                Type = ParseEnum(dto.Type, ProjectType.Calibration),
                DepartmentId = dto.DepartmentId,
                IsActive = dto.IsActive ?? true,
                CreatedBy = userId, UpdatedBy = userId,
            });
        }
        else
        {
            template = await templates.GetByIdAsync(templateId)
                ?? throw new KeyNotFoundException($"Template {templateId} not found.");
            template.Name = dto.Name.Trim();
            template.Description = dto.Description;
            template.Type = ParseEnum(dto.Type, template.Type);
            template.DepartmentId = dto.DepartmentId;
            if (dto.IsActive.HasValue) template.IsActive = dto.IsActive.Value;
            template.UpdatedBy = userId;
            template.UpdatedAt = DateTime.UtcNow;
            await templates.UpdateAsync(template);

            // Replace the children wholesale. A template is edited as a document, and diffing rows
            // would leave orphans behind whenever a milestone is renamed rather than moved.
            await SoftDeleteChildrenAsync(template.Id, userId);
        }

        foreach (var (m, i) in (dto.Milestones ?? []).Select((m, i) => (m, i)))
        {
            var created = await templateMilestones.CreateAsync(new ProjectTemplateMilestone
            {
                TemplateId = template.Id,
                Title = m.Title.Trim(), Description = m.Description,
                Order = m.Order > 0 ? m.Order : i + 1,
                OffsetDays = Math.Max(0, m.OffsetDays),
                DurationDays = Math.Max(0, m.DurationDays),
                ValuePct = m.ValuePct, IsBillable = m.IsBillable ?? true,
                CreatedBy = userId, UpdatedBy = userId,
            });

            foreach (var (t, j) in (m.Tasks ?? []).Select((t, j) => (t, j)))
                await templateTasks.CreateAsync(new ProjectTemplateTask
                {
                    TemplateMilestoneId = created.Id,
                    Title = t.Title.Trim(), Description = t.Description,
                    Order = t.Order > 0 ? t.Order : j + 1,
                    EstimatedHours = t.EstimatedHours,
                    CreatedBy = userId, UpdatedBy = userId,
                });
        }

        foreach (var l in dto.BudgetLines ?? [])
            await templateLines.CreateAsync(new ProjectTemplateBudgetLine
            {
                TemplateId = template.Id,
                Category = ParseEnum(l.Category, BudgetCategory.Other),
                Description = l.Description?.Trim() ?? string.Empty,
                RateCode = l.RateCode,
                Quantity = l.Quantity, UnitCostRate = l.UnitCostRate, UnitClientRate = l.UnitClientRate,
                CreatedBy = userId, UpdatedBy = userId,
            });

        return (await GetByIdAsync(template.Id))!;
    }

    public async Task DeleteAsync(string templateId, string userId)
    {
        var t = await templates.GetByIdAsync(templateId)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");
        // Deactivate rather than remove: projects raised from it still point back here, and a
        // template that produced real work is part of how those projects came to look as they do.
        t.IsActive = false;
        t.UpdatedBy = userId;
        t.UpdatedAt = DateTime.UtcNow;
        await templates.UpdateAsync(t);
    }

    // ── Instantiation ────────────────────────────────────────────────────────────

    public async Task<string> InstantiateAsync(string templateId, InstantiateTemplateDto dto, string userId)
    {
        var detail = await GetByIdAsync(templateId)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("The new project needs a name.");

        var template = (await templates.GetByIdAsync(templateId))!;
        var start = (dto.StartDate ?? DateTime.UtcNow).Date;

        var project = await projects.CreateAsync(new Project
        {
            Name = dto.Name.Trim(),
            ClientName = dto.ClientName,
            ClientId = dto.ClientId,
            Type = template.Type,
            Status = ProjectStatus.Draft,          // never Active — a generated plan still needs sign-off
            DepartmentId = dto.DepartmentId ?? template.DepartmentId ?? string.Empty,
            ProjectManagerId = dto.ProjectManagerId ?? userId,
            ContractValue = dto.ContractValue,
            StartDate = start,
            ExpectedEndDate = start.AddDays(MaxEndOffset(detail)),
            ScopeSummary = dto.ScopeSummary ?? template.Description,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await InstantiateInto(project, detail, start, dto.ContractValue, userId);

        template.UseCount += 1;
        template.UpdatedAt = DateTime.UtcNow;
        await templates.UpdateAsync(template);

        await history.CreateAsync(new ProjectHistory
        {
            ProjectId = project.Id,
            Action = "CreatedFromTemplate",
            NewValue = template.Name,
            Notes = $"Raised from template '{template.Name}': {detail.Milestones.Count} milestone(s), {detail.BudgetLines.Count} budget line(s).",
            ChangedByUserId = userId, CreatedBy = userId, UpdatedBy = userId,
        });

        return project.Id;
    }

    /// <summary>
    /// Writes a template's milestones, tasks and a draft budget onto a project. Shared by the manual
    /// route and the recurrence sweep, so a generated project and a hand-raised one are identical.
    /// </summary>
    public async Task InstantiateInto(
        Project project, ProjectTemplateDetailDto detail, DateTime start, decimal contractValue, string userId)
    {
        foreach (var tm in detail.Milestones)
        {
            var due = start.AddDays(tm.OffsetDays + tm.DurationDays);
            var milestone = await milestones.CreateAsync(new Milestone
            {
                ProjectId = project.Id,
                Title = tm.Title,
                Description = tm.Description,
                Order = tm.Order,
                StartDate = start.AddDays(tm.OffsetDays),
                DueDate = due,
                PlannedAmount = contractValue > 0m ? decimal.Round(contractValue * tm.ValuePct / 100m, 2) : null,
                IsBillable = tm.IsBillable,
                Status = MilestoneStatus.NotStarted,
                CreatedBy = userId, UpdatedBy = userId,
            });

            foreach (var tt in tm.Tasks)
                await tasks.CreateAsync(new ProjectTask
                {
                    ProjectId = project.Id,
                    MilestoneId = milestone.Id,
                    Title = tt.Title,
                    Description = tt.Description,
                    EstimatedHours = tt.EstimatedHours,
                    // Status is left at its default — ProjectTask.Status is derived from the linked
                    // assignment and managed by the service, not set from outside.
                    CreatedBy = userId, UpdatedBy = userId,
                });
        }

        if (detail.BudgetLines.Count == 0) return;

        // A DRAFT budget version, not an approved one: PR1 made budget approval the act that sets the
        // baseline, and a template must not be able to baseline a project nobody has looked at.
        var version = await budgetVersions.CreateAsync(new BudgetVersion
        {
            ProjectId = project.Id,
            VersionNo = 1,
            Status = BudgetVersionStatus.Draft,
            RevisionReason = null,
            CreatedBy = userId, UpdatedBy = userId,
        });

        decimal planned = 0m, quoted = 0m;
        foreach (var l in detail.BudgetLines)
        {
            var linePlanned = decimal.Round(l.Quantity * l.UnitCostRate, 2);
            var lineQuoted  = decimal.Round(l.Quantity * l.UnitClientRate, 2);
            planned += linePlanned;
            quoted  += lineQuoted;

            await budgetLines.CreateAsync(new BudgetLine
            {
                ProjectId = project.Id,
                BudgetVersionId = version.Id,
                Category = ParseEnum(l.Category, BudgetCategory.Other),
                Description = l.Description,
                Quantity = l.Quantity,
                UnitCostRate = l.UnitCostRate,
                PlannedAmount = linePlanned,
                QuotedAmount = lineQuoted,
                CreatedBy = userId, UpdatedBy = userId,
            });
        }

        version.TotalPlanned = planned;
        version.TotalQuoted  = quoted;
        version.UpdatedAt = DateTime.UtcNow;
        await budgetVersions.UpdateAsync(version);

        project.PlannedBudget = planned;
        project.UpdatedAt = DateTime.UtcNow;
        await projects.UpdateAsync(project);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static int MaxEndOffset(ProjectTemplateDetailDto d) =>
        d.Milestones.Count == 0 ? 30 : d.Milestones.Max(m => m.OffsetDays + m.DurationDays);

    private async Task SoftDeleteChildrenAsync(string templateId, string userId)
    {
        var ms = await templateMilestones.Query().Where(m => m.TemplateId == templateId && !m.IsDeleted).ToListAsync();
        var msIds = ms.Select(m => m.Id).ToList();
        var ts = await templateTasks.Query().Where(t => msIds.Contains(t.TemplateMilestoneId) && !t.IsDeleted).ToListAsync();
        var lines = await templateLines.Query().Where(l => l.TemplateId == templateId && !l.IsDeleted).ToListAsync();

        foreach (var t in ts)    { t.IsDeleted = true; t.UpdatedBy = userId; t.UpdatedAt = DateTime.UtcNow; await templateTasks.UpdateAsync(t); }
        foreach (var m in ms)    { m.IsDeleted = true; m.UpdatedBy = userId; m.UpdatedAt = DateTime.UtcNow; await templateMilestones.UpdateAsync(m); }
        foreach (var l in lines) { l.IsDeleted = true; l.UpdatedBy = userId; l.UpdatedAt = DateTime.UtcNow; await templateLines.UpdateAsync(l); }
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
}
