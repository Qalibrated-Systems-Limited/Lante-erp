using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using OperationsService.Core.DTOs.Approvals;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Milestones;
using OperationsService.Core.DTOs.Projects;
using OperationsService.Core.DTOs.Resources;
using OperationsService.Core.DTOs.Tasks;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public class ProjectService : IProjectService
{
    private readonly IGenericRepository<Project> _projects;
    private readonly IGenericRepository<Milestone> _milestones;
    private readonly IGenericRepository<ProjectTask> _tasks;
    private readonly IGenericRepository<ProjectApproval> _approvals;
    private readonly IGenericRepository<BudgetLine> _budgetLines;
    private readonly IGenericRepository<CostEntry> _costEntries;
    private readonly IGenericRepository<ProjectResource> _resources;
    private readonly IGenericRepository<Assignment> _assignments;
    private readonly IGenericRepository<AssignedTechnician> _technicians;
    private readonly IGenericRepository<ProjectAlertLog> _alerts;
    private readonly IGenericRepository<MilestoneUpdateLog> _milestoneUpdates;
    private readonly IGenericRepository<ProjectHistory> _history;
    private readonly IGenericRepository<ProjectDailyReport> _dailyReports;
    private readonly ICrmCustomerDirectory _crm;
    private readonly IFinanceGateway _finance;
    private readonly IProjectScheduleService _schedule;
    private readonly IMapper _mapper;

    public ProjectService(
        IGenericRepository<Project> projects,
        IGenericRepository<Milestone> milestones,
        IGenericRepository<ProjectTask> tasks,
        IGenericRepository<ProjectApproval> approvals,
        IGenericRepository<BudgetLine> budgetLines,
        IGenericRepository<CostEntry> costEntries,
        IGenericRepository<ProjectResource> resources,
        IGenericRepository<Assignment> assignments,
        IGenericRepository<AssignedTechnician> technicians,
        IGenericRepository<ProjectAlertLog> alerts,
        IGenericRepository<MilestoneUpdateLog> milestoneUpdates,
        IGenericRepository<ProjectHistory> history,
        IGenericRepository<ProjectDailyReport> dailyReports,
        ICrmCustomerDirectory crm,
        IFinanceGateway finance,
        IProjectScheduleService schedule,
        IMapper mapper)
    {
        _projects = projects;
        _milestones = milestones;
        _tasks = tasks;
        _approvals = approvals;
        _budgetLines = budgetLines;
        _costEntries = costEntries;
        _resources = resources;
        _assignments = assignments;
        _technicians = technicians;
        _alerts = alerts;
        _milestoneUpdates = milestoneUpdates;
        _history = history;
        _dailyReports = dailyReports;
        _crm = crm;
        _finance = finance;
        _schedule = schedule;
        _mapper = mapper;
    }

    // O2 — budget-burn alert history for a project (written by the background sweep).
    public async Task<IEnumerable<ProjectAlertLogReadDto>> GetProjectAlertsAsync(string projectId)
    {
        var alerts = await _alerts.Query()
            .Where(a => a.ProjectId == projectId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return _mapper.Map<List<ProjectAlertLogReadDto>>(alerts);
    }

    // Project audit trail — lifecycle transitions are recorded here as they happen.
    public async Task<IEnumerable<ProjectHistoryReadDto>> GetProjectHistoryAsync(string projectId)
    {
        var entries = await _history.Query()
            .Where(h => h.ProjectId == projectId && !h.IsDeleted)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
        return _mapper.Map<List<ProjectHistoryReadDto>>(entries);
    }

    // Writes one audit-trail entry. Best-effort: never blocks the primary action.
    private async Task LogHistoryAsync(string projectId, string userId, string action,
        string? oldValue = null, string? newValue = null, string? notes = null)
    {
        await _history.CreateAsync(new ProjectHistory
        {
            ProjectId = projectId,
            ChangedByUserId = userId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Notes = notes,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId
        });
    }

    // Project-level daily site reports.
    public async Task<IEnumerable<ProjectDailyReportReadDto>> GetDailyReportsAsync(string projectId)
    {
        var reports = await _dailyReports.Query()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .OrderByDescending(r => r.ReportDate)
            .ToListAsync();
        return _mapper.Map<List<ProjectDailyReportReadDto>>(reports);
    }

    public async Task<ProjectDailyReportReadDto> AddDailyReportAsync(string projectId, CreateProjectDailyReportDto dto, string userId)
    {
        _ = await _projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var report = _mapper.Map<ProjectDailyReport>(dto);
        report.ProjectId = projectId;
        report.SubmittedByUserId = userId;
        report.IsReviewed = false;
        report.CreatedBy = userId;
        report.UpdatedBy = userId;
        var created = await _dailyReports.CreateAsync(report);
        return _mapper.Map<ProjectDailyReportReadDto>(created);
    }

    public async Task<ProjectReadDto?> GetByIdAsync(string id)
    {
        var project = await _projects.Query()
            .Include(p => p.Milestones).ThenInclude(m => m.Tasks)
            .Include(p => p.Approvals)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (project is null) return null;

        var dto = _mapper.Map<ProjectReadDto>(project);
        // Populate nested milestones (ordered, each with its tasks) for the detail view.
        dto.Milestones = _mapper.Map<List<MilestoneReadDto>>(project.Milestones.OrderBy(m => m.Order));
        // Populate approvals (newest first) so the detail view can render + process them.
        dto.Approvals = _mapper.Map<List<ProjectApprovalReadDto>>(project.Approvals.OrderByDescending(a => a.CreatedAt));
        return dto;
    }

    public async Task<PaginatedResult<ProjectReadDto>> GetAllAsync(ProjectFilterParameters filters, string? departmentId)
    {
        // No Include(Milestones).ThenInclude(Tasks) here: that eagerly hydrated every milestone
        // and task for every project on the page just to compute MilestoneCount/TaskCount. Those
        // are fetched below as two cheap grouped-count queries scoped to this page's project IDs
        // instead — the list view never needs the actual milestone/task rows (Milestones/Approvals
        // stay Ignore()'d in the AutoMapper profile for this DTO; only GetByIdAsync populates them).
        var query = _projects.Query()
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrEmpty(filters.MemberUserId))
            query = query.Where(p =>
                p.ProjectManagerId == filters.MemberUserId ||
                _resources.Query().Any(r => r.ProjectId == p.Id && r.UserId == filters.MemberUserId && !r.IsDeleted));
        else if (filters.DepartmentIds is { Count: > 0 })
            query = query.Where(p => p.DepartmentId != null && filters.DepartmentIds.Contains(p.DepartmentId));
        else if (!string.IsNullOrEmpty(departmentId))
            query = query.Where(p => p.DepartmentId == departmentId);

        if (!string.IsNullOrEmpty(filters.Search))
            // ILike (not Contains) so Npgsql translates to a form the GIN trigram indexes on
            // Name/ClientName can actually serve — Contains()'s LIKE '%term%' has a leading
            // wildcard that no ordinary index, and no plain LIKE either, can use (#279).
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{filters.Search}%")
                || (p.ClientName != null && EF.Functions.ILike(p.ClientName, $"%{filters.Search}%")));

        if (!string.IsNullOrEmpty(filters.Status) && Enum.TryParse<ProjectStatus>(filters.Status, out var status))
            query = query.Where(p => p.Status == status);

        if (!string.IsNullOrEmpty(filters.Type) && Enum.TryParse<ProjectType>(filters.Type, out var type))
            query = query.Where(p => p.Type == type);

        query = filters.SortDescending
            ? query.OrderByDescending(p => p.CreatedAt)
            : query.OrderBy(p => p.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        var projectIds = items.Select(p => p.Id).ToList();
        var milestoneCounts = await _milestones.Query()
            .Where(m => !m.IsDeleted && projectIds.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);
        var taskCounts = await _tasks.Query()
            .Where(t => !t.IsDeleted && projectIds.Contains(t.ProjectId))
            .GroupBy(t => t.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        var dtos = _mapper.Map<List<ProjectReadDto>>(items);
        foreach (var dto in dtos)
        {
            dto.MilestoneCount = milestoneCounts.GetValueOrDefault(dto.Id);
            dto.TaskCount = taskCounts.GetValueOrDefault(dto.Id);
        }

        return new PaginatedResult<ProjectReadDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize
        };
    }

    public async Task<ProjectReadDto> CreateAsync(CreateProjectDto dto, string managerId, string departmentId)
    {
        // O1 — CRM CUSTOMER verify seam: if the project is linked to a CRM lead, confirm it exists
        // (no-op / passes when CRM verification is disabled).
        if (!string.IsNullOrWhiteSpace(dto.CrmLeadId) && !await _crm.CustomerExistsAsync(dto.CrmLeadId))
            throw new InvalidOperationException($"CRM lead '{dto.CrmLeadId}' could not be verified.");

        var project = _mapper.Map<Project>(dto);
        project.ProjectManagerId = managerId;
        project.DepartmentId = !string.IsNullOrEmpty(departmentId) ? departmentId : dto.DepartmentId;
        project.Status = ProjectStatus.Draft;
        project.CreatedBy = managerId;
        project.UpdatedBy = managerId;
        var created = await _projects.CreateAsync(project);
        await LogHistoryAsync(created.Id, managerId, "Project created", newValue: "Draft");
        return _mapper.Map<ProjectReadDto>(created);
    }

    public async Task<ProjectReadDto> UpdateAsync(string id, UpdateProjectDto dto, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");
        _mapper.Map(dto, project);
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;
        var updated = await _projects.UpdateAsync(project);
        return _mapper.Map<ProjectReadDto>(updated);
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");
        project.IsDeleted = true;
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;
        await _projects.UpdateAsync(project);
    }

    public async Task<ProjectReadDto> SubmitForApprovalAsync(string id, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");
        if (project.Status != ProjectStatus.Draft)
            throw new InvalidOperationException("Only draft projects can be submitted for approval.");

        project.Status = ProjectStatus.PendingMdApproval;
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;

        var approval = new ProjectApproval
        {
            ProjectId = id,
            ApprovalType = ApprovalType.MD,
            Status = ApprovalStatus.Pending,
            RequestedBy = userId,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        await _approvals.CreateAsync(approval);
        var updated = await _projects.UpdateAsync(project);
        await LogHistoryAsync(id, userId, "Submitted for approval", "Draft", "Pending MD Approval");
        return _mapper.Map<ProjectReadDto>(updated);
    }

    public async Task<ProjectApprovalReadDto> ReviewMdApprovalAsync(string id, ReviewProjectApprovalDto dto, string reviewerId)
    {
        var approval = await _approvals.Query()
            .FirstOrDefaultAsync(a => a.ProjectId == id && a.ApprovalType == ApprovalType.MD && a.Status == ApprovalStatus.Pending)
            ?? throw new KeyNotFoundException("Pending MD approval not found.");

        approval.Status = dto.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ReviewedBy = reviewerId;
        approval.ReviewedAt = DateTime.UtcNow;
        approval.Comments = dto.Comments;
        approval.UpdatedBy = reviewerId;
        approval.UpdatedAt = DateTime.UtcNow;
        await _approvals.UpdateAsync(approval);

        var project = await _projects.GetByIdAsync(id);
        if (project != null)
        {
            project.Status = dto.Approved ? ProjectStatus.PendingFinanceApproval : ProjectStatus.Draft;
            project.UpdatedBy = reviewerId;
            project.UpdatedAt = DateTime.UtcNow;

            if (dto.Approved)
            {
                var financeApproval = new ProjectApproval
                {
                    ProjectId = id,
                    ApprovalType = ApprovalType.Finance,
                    Status = ApprovalStatus.Pending,
                    RequestedBy = reviewerId,
                    CreatedBy = reviewerId,
                    UpdatedBy = reviewerId
                };
                await _approvals.CreateAsync(financeApproval);
            }
            await _projects.UpdateAsync(project);
            await LogHistoryAsync(id, reviewerId,
                dto.Approved ? "MD approved" : "MD rejected",
                "Pending MD Approval",
                dto.Approved ? "Pending Finance Approval" : "Draft",
                dto.Comments);
        }
        return _mapper.Map<ProjectApprovalReadDto>(approval);
    }

    public async Task<ProjectApprovalReadDto> ReviewFinanceApprovalAsync(string id, ReviewProjectApprovalDto dto, string reviewerId)
    {
        var approval = await _approvals.Query()
            .FirstOrDefaultAsync(a => a.ProjectId == id && a.ApprovalType == ApprovalType.Finance && a.Status == ApprovalStatus.Pending)
            ?? throw new KeyNotFoundException("Pending Finance approval not found.");

        approval.Status = dto.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.ReviewedBy = reviewerId;
        approval.ReviewedAt = DateTime.UtcNow;
        approval.Comments = dto.Comments;
        approval.UpdatedBy = reviewerId;
        approval.UpdatedAt = DateTime.UtcNow;
        await _approvals.UpdateAsync(approval);

        var project = await _projects.GetByIdAsync(id);
        if (project != null)
        {
            project.Status = dto.Approved ? ProjectStatus.Planning : ProjectStatus.Draft;
            if (dto.Approved && dto.AllocatedBudget.HasValue)
                project.PlannedBudget = dto.AllocatedBudget.Value;
            project.UpdatedBy = reviewerId;
            project.UpdatedAt = DateTime.UtcNow;
            await _projects.UpdateAsync(project);
            await LogHistoryAsync(id, reviewerId,
                dto.Approved ? "Finance approved" : "Finance rejected",
                "Pending Finance Approval",
                dto.Approved ? "Planning" : "Draft",
                dto.Comments);
        }
        return _mapper.Map<ProjectApprovalReadDto>(approval);
    }

    public async Task<ProjectReadDto> PutOnHoldAsync(string id, string reason, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");
        var prev = project.Status.ToString();
        project.Status = ProjectStatus.OnHold;
        project.Notes = string.IsNullOrEmpty(project.Notes) ? reason : $"{project.Notes}\n[On Hold]: {reason}";
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;
        var result = _mapper.Map<ProjectReadDto>(await _projects.UpdateAsync(project));
        await LogHistoryAsync(id, userId, "Put on hold", prev, "On Hold", reason);
        return result;
    }

    public async Task<ProjectReadDto> ResumeAsync(string id, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");
        var prev = project.Status.ToString();
        project.Status = ProjectStatus.Active;
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;
        var result = _mapper.Map<ProjectReadDto>(await _projects.UpdateAsync(project));
        await LogHistoryAsync(id, userId, "Resumed", prev, "Active");
        return result;
    }

    public async Task<ProjectReadDto> CloseAsync(string id, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");
        var prev = project.Status.ToString();
        project.Status = ProjectStatus.Closed;
        project.ActualEndDate = DateTime.UtcNow;
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;
        var result = _mapper.Map<ProjectReadDto>(await _projects.UpdateAsync(project));
        await LogHistoryAsync(id, userId, "Closed", prev, "Closed");
        return result;
    }

    // O1 — MD-activation gate: a fully-approved project (Planning) is activated before any work,
    // expenditure or timesheet may post. Only Planning → Active is allowed here.
    public async Task<ProjectReadDto> ActivateAsync(string id, string userId)
    {
        var project = await _projects.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        if (project.Status == ProjectStatus.Active)
            throw new InvalidOperationException("Project is already active.");
        if (project.Status != ProjectStatus.Planning)
            throw new InvalidOperationException(
                $"Only an approved project (Planning) can be activated. Current status: {project.Status}.");

        project.Status      = ProjectStatus.Active;
        project.ActivatedAt = DateTime.UtcNow;
        project.UpdatedBy   = userId;
        project.UpdatedAt   = DateTime.UtcNow;
        await _projects.UpdateAsync(project);

        // PR1 — every Active project should have a plan of record. An approved budget already sets
        // one (that is the primary path); this covers a project activated before its budget was
        // priced, so variance is never silently unmeasurable. Existing baselines are left alone —
        // moving one is a change-request decision, not a side effect of activation.
        if (project.BaselineSetAt is null)
        {
            try { await _schedule.SetBaselineAsync(id, userId); }
            catch (Exception) { /* baselining must never block activation */ }
        }

        var result = _mapper.Map<ProjectReadDto>((await _projects.GetByIdAsync(id))!);
        await LogHistoryAsync(id, userId, "Activated", "Planning", "Active");
        return result;
    }

    // Milestones
    public async Task<MilestoneReadDto?> GetMilestoneByIdAsync(string milestoneId)
    {
        var m = await _milestones.Query()
            .Include(x => x.Tasks)
            .FirstOrDefaultAsync(x => x.Id == milestoneId && !x.IsDeleted);
        return m is null ? null : _mapper.Map<MilestoneReadDto>(m);
    }

    public async Task<IEnumerable<MilestoneReadDto>> GetMilestonesAsync(string projectId)
    {
        var milestones = await _milestones.Query()
            .Include(m => m.Tasks)
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Order)
            .ToListAsync();
        return _mapper.Map<List<MilestoneReadDto>>(milestones);
    }

    public async Task<MilestoneReadDto> CreateMilestoneAsync(string projectId, CreateMilestoneDto dto, string userId)
    {
        var milestone = _mapper.Map<Milestone>(dto);
        milestone.ProjectId = projectId;
        milestone.Status = MilestoneStatus.NotStarted;
        milestone.CreatedBy = userId;
        milestone.UpdatedBy = userId;
        var created = await _milestones.CreateAsync(milestone);
        return _mapper.Map<MilestoneReadDto>(created);
    }

    public async Task<MilestoneReadDto> UpdateMilestoneAsync(string milestoneId, UpdateMilestoneDto dto, string userId)
    {
        var milestone = await _milestones.GetByIdAsync(milestoneId)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");
        _mapper.Map(dto, milestone);
        // O1 — record the milestone-progress timestamp (drives the O3 daily 5PM staleness check).
        milestone.LastUpdatedAt = DateTime.UtcNow;
        milestone.UpdatedBy = userId;
        milestone.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<MilestoneReadDto>(await _milestones.UpdateAsync(milestone));
    }

    // O1 — client sign-off on a milestone. Captures who accepted + when, and marks it Completed. This
    // is the hook the O3 invoicing seam consumes (billable + signed-off → Finance invoice trigger).
    public async Task<MilestoneReadDto> SignOffMilestoneAsync(string milestoneId, SignOffMilestoneDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.SignOffBy))
            throw new InvalidOperationException("Sign-off requires the name of the accepting party.");

        var milestone = await _milestones.GetByIdAsync(milestoneId)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");

        if (milestone.SignOffAt != null)
            throw new InvalidOperationException("This milestone has already been signed off.");

        milestone.SignOffBy     = dto.SignOffBy.Trim();
        milestone.SignOffAt     = DateTime.UtcNow;
        milestone.Status        = MilestoneStatus.Completed;
        milestone.ProgressPct   = 100;
        milestone.LastUpdatedAt = DateTime.UtcNow;
        milestone.UpdatedBy     = userId;
        milestone.UpdatedAt     = DateTime.UtcNow;
        await _milestones.UpdateAsync(milestone);

        // O3 — client sign-off is the invoicing/revenue trigger. Billable milestones raise a Finance
        // invoice; every signed-off milestone reports 100% completion for IFRS-15 revenue recognition.
        // Config-gated seam (no-op until finance-service is wired) — failures never block sign-off.
        var amount = milestone.PlannedAmount ?? 0m;
        var project = await _projects.GetByIdAsync(milestone.ProjectId);
        try
        {
            if (milestone.IsBillable && amount > 0m)
                await _finance.RaiseMilestoneInvoiceAsync(new MilestoneInvoiceRequest(
                    milestone.ProjectId, milestone.Id, milestone.Title, amount, project?.ClientName, userId,
                    project?.ClientId));

            await _finance.ReportRevenueRecognitionAsync(new RevenueRecognitionRequest(
                milestone.ProjectId, milestone.Id, 100, amount));
        }
        catch (Exception)
        {
            // seam failure is non-fatal to sign-off; the no-op never throws, a real client is best-effort
        }

        return _mapper.Map<MilestoneReadDto>(milestone);
    }

    // O3 — daily milestone progress update (the "by 5PM" requirement). Appends a MILESTONE_UPDATE_LOG
    // row and refreshes LastUpdatedAt so the 5PM breach sweep sees the milestone as current.
    public async Task<MilestoneUpdateLogReadDto> AddMilestoneUpdateAsync(string milestoneId, AddMilestoneUpdateDto dto, string userId)
    {
        var milestone = await _milestones.GetByIdAsync(milestoneId)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");

        if (dto.Status.HasValue) milestone.Status = dto.Status.Value;
        if (dto.ProgressPct.HasValue)
            milestone.ProgressPct = Math.Clamp(dto.ProgressPct.Value, 0, 100);
        milestone.LastUpdatedAt = DateTime.UtcNow;
        milestone.UpdatedBy = userId;
        milestone.UpdatedAt = DateTime.UtcNow;
        await _milestones.UpdateAsync(milestone);

        var log = await _milestoneUpdates.CreateAsync(new MilestoneUpdateLog
        {
            MilestoneId     = milestone.Id,
            ProjectId       = milestone.ProjectId,
            Note            = dto.Note?.Trim(),
            StatusAtUpdate  = milestone.Status.ToString(),
            ProgressPct     = milestone.ProgressPct,
            IsBreachAlert   = false,
            UpdatedByUserId = userId,
            CreatedBy       = userId,
            UpdatedBy       = userId,
        });
        return _mapper.Map<MilestoneUpdateLogReadDto>(log);
    }

    public async Task<IEnumerable<MilestoneUpdateLogReadDto>> GetMilestoneUpdatesAsync(string milestoneId)
    {
        var logs = await _milestoneUpdates.Query()
            .Where(u => u.MilestoneId == milestoneId && !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
        return _mapper.Map<List<MilestoneUpdateLogReadDto>>(logs);
    }

    public async Task DeleteMilestoneAsync(string milestoneId, string userId)
    {
        var milestone = await _milestones.GetByIdAsync(milestoneId)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");
        milestone.IsDeleted = true;
        milestone.UpdatedBy = userId;
        milestone.UpdatedAt = DateTime.UtcNow;
        await _milestones.UpdateAsync(milestone);
    }

    // Tasks
    public async Task<TaskReadDto?> GetTaskByIdAsync(string taskId)
    {
        var task = await _tasks.GetByIdAsync(taskId);
        return task is null ? null : _mapper.Map<TaskReadDto>(task);
    }

    public async Task<IEnumerable<TaskReadDto>> GetTasksAsync(string milestoneId)
    {
        var tasks = await _tasks.Query()
            .Where(t => t.MilestoneId == milestoneId && !t.IsDeleted)
            .ToListAsync();
        return _mapper.Map<List<TaskReadDto>>(tasks);
    }

    public async Task<TaskReadDto> CreateTaskAsync(string milestoneId, CreateTaskDto dto, string userId)
    {
        var milestone = await _milestones.GetByIdAsync(milestoneId)
            ?? throw new KeyNotFoundException($"Milestone {milestoneId} not found.");

        // PR1 — subtasks are one level deep. A grandchild makes roll-up ambiguous (does the parent's
        // progress include it once, twice, or not at all?) and is not how work here is broken down.
        if (!string.IsNullOrWhiteSpace(dto.ParentTaskId))
        {
            var parent = await _tasks.GetByIdAsync(dto.ParentTaskId!)
                ?? throw new KeyNotFoundException($"Parent task {dto.ParentTaskId} not found.");
            if (parent.MilestoneId != milestoneId)
                throw new InvalidOperationException("A subtask must sit under the same milestone as its parent.");
            if (!string.IsNullOrWhiteSpace(parent.ParentTaskId))
                throw new InvalidOperationException(
                    $"'{parent.Title}' is already a subtask. Tasks nest one level only — add this under its parent instead.");
        }

        var task = _mapper.Map<ProjectTask>(dto);
        task.MilestoneId = milestoneId;
        task.ProjectId = milestone.ProjectId;
        task.Status = Enums.TaskStatus.NotStarted;
        task.CreatedBy = userId;
        task.UpdatedBy = userId;
        var created = await _tasks.CreateAsync(task);

        // Auto-dispatch: a task created already assigned to staff becomes an Operations Assignment.
        if (!string.IsNullOrWhiteSpace(dto.AssignedToUserId))
            await DispatchTaskInternalAsync(created, dto.AssignedToUserId, null, null, userId, string.Empty);

        var result = await _tasks.GetByIdAsync(created.Id);
        return _mapper.Map<TaskReadDto>(result!);
    }

    public async Task<TaskReadDto> UpdateTaskAsync(string taskId, UpdateTaskDto dto, string userId)
    {
        var task = await _tasks.GetByIdAsync(taskId)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");
        var wasUnlinked  = string.IsNullOrEmpty(task.LinkedAssignmentId);
        var prevAssignee = task.AssignedToUserId;
        _mapper.Map(dto, task);
        task.UpdatedBy = userId;
        task.UpdatedAt = DateTime.UtcNow;
        await _tasks.UpdateAsync(task);

        if (wasUnlinked && !string.IsNullOrWhiteSpace(task.AssignedToUserId))
        {
            // Auto-dispatch: assigning a previously-unassigned task to staff creates its Assignment.
            await DispatchTaskInternalAsync(task, task.AssignedToUserId, null, null, userId, string.Empty);
        }
        else if (!wasUnlinked && !string.IsNullOrWhiteSpace(task.AssignedToUserId)
                 && !string.Equals(task.AssignedToUserId, prevAssignee, StringComparison.Ordinal))
        {
            // Reassignment: an already-dispatched task moved to a different technician.
            await ReassignAssignmentTechnicianAsync(task.LinkedAssignmentId!, task.AssignedToUserId, null, userId);
        }

        await SyncMilestoneStatusAsync(task.MilestoneId, userId);
        var updated = await _tasks.GetByIdAsync(taskId);
        return _mapper.Map<TaskReadDto>(updated!);
    }

    public async Task<TaskReadDto> DispatchTaskAsync(string taskId, DispatchTaskDto dto, string userId, string departmentId)
    {
        var task = await _tasks.GetByIdAsync(taskId)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        if (string.IsNullOrEmpty(task.LinkedAssignmentId))
        {
            // Not yet dispatched → create the linked Assignment.
            await DispatchTaskInternalAsync(task, dto.AssignedToUserId, dto.AssignedToUserName, dto.Notes, userId, departmentId);
        }
        else if (!string.IsNullOrWhiteSpace(dto.AssignedToUserId)
                 && !string.Equals(dto.AssignedToUserId, task.AssignedToUserId, StringComparison.Ordinal))
        {
            // Already dispatched → treat as a reassignment to a different technician.
            task.AssignedToUserId = dto.AssignedToUserId;
            task.UpdatedBy = userId;
            task.UpdatedAt = DateTime.UtcNow;
            await _tasks.UpdateAsync(task);
            await ReassignAssignmentTechnicianAsync(task.LinkedAssignmentId!, dto.AssignedToUserId, dto.AssignedToUserName, userId);
        }

        var updated = await _tasks.GetByIdAsync(taskId);
        return _mapper.Map<TaskReadDto>(updated!);
    }

    // Moves a dispatched task's linked Assignment to a different technician:
    // soft-removes the current active technician(s) and records the new one.
    private async Task ReassignAssignmentTechnicianAsync(string assignmentId, string newUserId, string? newUserName, string userId)
    {
        var existing = await _technicians.Query()
            .Where(t => t.AssignmentId == assignmentId && !t.IsDeleted)
            .ToListAsync();
        if (existing.Any(t => t.UserId == newUserId))
            return; // already assigned to this person

        foreach (var t in existing)
        {
            t.IsDeleted = true;
            t.UpdatedBy = userId;
            t.UpdatedAt = DateTime.UtcNow;
            await _technicians.UpdateAsync(t);
        }
        await _technicians.CreateAsync(new AssignedTechnician
        {
            AssignmentId = assignmentId,
            UserId = newUserId,
            UserName = newUserName ?? newUserId,
            AssignedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId
        });
    }

    // Turns a project task into an Operations Assignment (SourceType = ProjectTask) and links them.
    // Shared by explicit dispatch AND auto-dispatch-on-assign (mirrors TicketService.AssignAsync).
    // No-op guard: a task already linked to an assignment is not dispatched again here.
    private async Task DispatchTaskInternalAsync(ProjectTask task, string? assignedUserId, string? assignedUserName,
        string? notes, string userId, string departmentId)
    {
        if (!string.IsNullOrEmpty(task.LinkedAssignmentId))
            return; // already dispatched

        // Use the project's department so the assignment appears in the correct department view.
        var project = await _projects.GetByIdAsync(task.ProjectId);
        var resolvedDepartmentId = !string.IsNullOrEmpty(project?.DepartmentId)
            ? project.DepartmentId
            : departmentId;

        if (!string.IsNullOrEmpty(assignedUserId))
            task.AssignedToUserId = assignedUserId;

        var assignment = await _assignments.CreateAsync(new Assignment
        {
            Title = task.Title,
            Description = string.IsNullOrEmpty(notes) ? task.Description : $"{task.Description}\n\n{notes}",
            SourceType = AssignmentSourceType.ProjectTask,
            LinkedProjectTaskId = task.Id,
            LinkedProjectId = task.ProjectId,
            LinkedMilestoneId = task.MilestoneId,
            DepartmentId = resolvedDepartmentId,
            ManagerId = userId,
            Status = AssignmentStatus.Accepted,
            CreatedBy = userId,
            UpdatedBy = userId
        });

        var techId = task.AssignedToUserId;
        if (!string.IsNullOrEmpty(techId))
        {
            await _technicians.CreateAsync(new AssignedTechnician
            {
                AssignmentId = assignment.Id,
                UserId = techId,
                UserName = assignedUserName ?? techId,
                AssignedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId
            });
        }

        task.LinkedAssignmentId = assignment.Id;
        task.Status = Enums.TaskStatus.InProgress;
        task.UpdatedBy = userId;
        task.UpdatedAt = DateTime.UtcNow;
        await _tasks.UpdateAsync(task);
        await SyncMilestoneStatusAsync(task.MilestoneId, userId);
    }

    private async Task SyncMilestoneStatusAsync(string milestoneId, string userId)
    {
        var milestone = await _milestones.GetByIdAsync(milestoneId);
        if (milestone is null) return;

        var tasks = await _tasks.Query()
            .Where(t => t.MilestoneId == milestoneId && !t.IsDeleted)
            .ToListAsync();

        if (tasks.Count == 0) return;

        MilestoneStatus newStatus;
        if (tasks.All(t => t.Status == Enums.TaskStatus.Done))
            newStatus = MilestoneStatus.Completed;
        else if (tasks.Any(t => t.Status == Enums.TaskStatus.InProgress || t.Status == Enums.TaskStatus.Blocked))
            newStatus = MilestoneStatus.InProgress;
        else
            newStatus = MilestoneStatus.NotStarted;

        if (milestone.Status == newStatus) return;

        milestone.Status = newStatus;
        milestone.UpdatedBy = userId;
        milestone.UpdatedAt = DateTime.UtcNow;
        await _milestones.UpdateAsync(milestone);
    }

    public async Task DeleteTaskAsync(string taskId, string userId)
    {
        var task = await _tasks.GetByIdAsync(taskId)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");
        task.IsDeleted = true;
        task.UpdatedBy = userId;
        task.UpdatedAt = DateTime.UtcNow;
        await _tasks.UpdateAsync(task);
    }

    // Budget
    public async Task<BudgetSummaryDto> GetBudgetSummaryAsync(string projectId)
    {
        var project = await _projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var lines = await _budgetLines.Query()
            .Where(b => b.ProjectId == projectId && !b.IsDeleted)
            .ToListAsync();

        var costEntries = await _costEntries.Query()
            .Include(c => c.Milestone)
            .Where(c => c.ProjectId == projectId && !c.IsDeleted)
            .OrderByDescending(c => c.EntryDate)
            .ToListAsync();

        // Compute actuals from cost entries — always accurate regardless of BudgetLine.ActualAmount state
        var actualByLine = costEntries
            .Where(c => c.BudgetLineId != null)
            .GroupBy(c => c.BudgetLineId!)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        var actualByMilestone = costEntries
            .Where(c => c.MilestoneId != null)
            .GroupBy(c => c.MilestoneId!)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        var totalActual = costEntries.Sum(c => c.Amount);

        var linesDtos = lines.Select(l =>
        {
            var actual = actualByLine.GetValueOrDefault(l.Id, 0);
            return new BudgetLineReadDto
            {
                Id = l.Id,
                ProjectId = l.ProjectId,
                Category = l.Category.ToString(),
                Description = l.Description,
                PlannedAmount = l.PlannedAmount,
                ActualAmount = actual,
                Variance = l.PlannedAmount - actual,
                CreatedAt = l.CreatedAt
            };
        }).ToList();

        var milestones = await _milestones.Query()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .OrderBy(m => m.Order)
            .ToListAsync();

        var milestoneBreakdown = milestones.Select(m =>
        {
            var actual = actualByMilestone.GetValueOrDefault(m.Id, 0);
            return new MilestoneBudgetDto
            {
                MilestoneId = m.Id,
                MilestoneTitle = m.Title,
                PlannedAmount = m.PlannedAmount ?? 0,
                ActualAmount = actual,
                Variance = (m.PlannedAmount ?? 0) - actual
            };
        }).ToList();

        var entriesDtos = _mapper.Map<List<CostEntryReadDto>>(costEntries);

        return new BudgetSummaryDto
        {
            ProjectId = projectId,
            PlannedBudget = project.PlannedBudget,
            TotalEstimated = lines.Sum(l => l.PlannedAmount),
            ActualCost = totalActual,
            Committed = project.Committed,
            Remaining = project.PlannedBudget - totalActual,
            // O2 — utilization is the burn exposure (spent + committed) against the planned budget.
            UtilizationPercent = project.PlannedBudget > 0 ? Math.Round((totalActual + project.Committed) / project.PlannedBudget * 100, 2) : 0,
            BudgetLocked = project.BudgetLocked,
            Lines = linesDtos,
            MilestoneBreakdown = milestoneBreakdown,
            CostEntries = entriesDtos
        };
    }

    public async Task<BudgetLineReadDto> CreateBudgetLineAsync(CreateBudgetLineDto dto, string userId)
    {
        var line = _mapper.Map<BudgetLine>(dto);
        line.CreatedBy = userId;
        line.UpdatedBy = userId;
        var created = await _budgetLines.CreateAsync(line);
        return _mapper.Map<BudgetLineReadDto>(created);
    }

    public async Task<BudgetLineReadDto> UpdateBudgetLineAsync(string lineId, UpdateBudgetLineDto dto, string userId)
    {
        var line = await _budgetLines.GetByIdAsync(lineId)
            ?? throw new KeyNotFoundException($"Budget line {lineId} not found.");
        if (dto.Description != null) line.Description = dto.Description;
        if (dto.PlannedAmount.HasValue) line.PlannedAmount = dto.PlannedAmount.Value;
        line.UpdatedBy = userId;
        line.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<BudgetLineReadDto>(await _budgetLines.UpdateAsync(line));
    }

    public async Task DeleteBudgetLineAsync(string lineId, string userId)
    {
        var line = await _budgetLines.GetByIdAsync(lineId)
            ?? throw new KeyNotFoundException($"Budget line {lineId} not found.");
        line.IsDeleted = true;
        line.UpdatedBy = userId;
        line.UpdatedAt = DateTime.UtcNow;
        await _budgetLines.UpdateAsync(line);
    }

    public async Task<CostEntryReadDto> AddCostEntryAsync(CreateCostEntryDto dto, string userId)
    {
        var project = await _projects.GetByIdAsync(dto.ProjectId)
            ?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");

        // O1 — MD-activation gate: no expenditure may post against a project that isn't Active.
        if (project.Status != ProjectStatus.Active)
            throw new InvalidOperationException(
                $"Costs can only be recorded against an active (approved) project. Current status: {project.Status}.");

        // O2 — hard block: once the budget is exhausted (100% burn) no further spend posts until revised.
        if (project.BudgetLocked)
            throw new InvalidOperationException(
                "Project budget is exhausted (100%). Expenditure is blocked until the budget is revised.");

        // O2 — same-day rule: a backdated expenditure requires a Finance-Manager approval reference.
        var today           = DateTime.UtcNow.Date;
        var expenditureDate = dto.ExpenditureDate?.ToUniversalTime() ?? DateTime.UtcNow;
        var isBackdated     = expenditureDate.Date < today;
        if (isBackdated && string.IsNullOrWhiteSpace(dto.FmApprovalRef))
            throw new InvalidOperationException(
                "Backdated expenditure requires a Finance-Manager approval reference (fmApprovalRef).");

        var entry = new CostEntry
        {
            ProjectId = dto.ProjectId,
            BudgetLineId = dto.BudgetLineId,
            MilestoneId = dto.MilestoneId,
            AssignmentId = dto.AssignmentId,
            Category = dto.Category,
            Description = dto.Description,
            Amount = dto.Amount,
            Notes = dto.Notes,
            EntryDate = expenditureDate,
            IsBackdated = isBackdated,
            FmApprovalRef = dto.FmApprovalRef?.Trim(),
            CreatedBy = userId,
            UpdatedBy = userId
        };
        await _costEntries.CreateAsync(entry);

        // Roll the spend into the linked budget line so BudgetLine.ActualAmount stays in sync
        if (dto.BudgetLineId is not null)
        {
            var line = await _budgetLines.GetByIdAsync(dto.BudgetLineId);
            if (line is not null)
            {
                line.ActualAmount += dto.Amount;
                line.UpdatedBy = userId;
                line.UpdatedAt = DateTime.UtcNow;
                await _budgetLines.UpdateAsync(line);
            }
        }

        // Keep Project.ActualCost current. O2 — apply the hard lock immediately if this tips burn to
        // 100% (the background sweep logs the alert; the block itself shouldn't wait for the sweep).
        project.ActualCost += dto.Amount;
        if (project.PlannedBudget > 0 && (project.ActualCost + project.Committed) >= project.PlannedBudget)
            project.BudgetLocked = true;
        project.UpdatedBy = userId;
        project.UpdatedAt = DateTime.UtcNow;
        await _projects.UpdateAsync(project);

        var readDto = _mapper.Map<CostEntryReadDto>(entry);
        readDto.RecordedByUserId = userId;
        return readDto;
    }

    // Resources
    public async Task<IEnumerable<ProjectResourceReadDto>> GetResourcesAsync(string projectId)
    {
        var resources = await _resources.Query()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted)
            .ToListAsync();
        return _mapper.Map<List<ProjectResourceReadDto>>(resources);
    }

    public async Task<ProjectResourceReadDto> AddResourceAsync(AddProjectResourceDto dto, string userId)
    {
        var resource = new ProjectResource
        {
            ProjectId = dto.ProjectId,
            UserId = dto.UserId,
            UserName = dto.UserName,
            Role = dto.Role,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        var created = await _resources.CreateAsync(resource);
        return _mapper.Map<ProjectResourceReadDto>(created);
    }

    public async Task<ProjectResourceReadDto> UpdateResourceAsync(string resourceId, UpdateProjectResourceDto dto, string userId)
    {
        var resource = await _resources.GetByIdAsync(resourceId)
            ?? throw new KeyNotFoundException($"Resource {resourceId} not found.");
        if (dto.Role != null) resource.Role = dto.Role;
        if (dto.IsActive.HasValue) resource.IsDeleted = !dto.IsActive.Value;
        resource.UpdatedBy = userId;
        resource.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<ProjectResourceReadDto>(await _resources.UpdateAsync(resource));
    }

    public async Task RemoveResourceAsync(string resourceId, string userId)
    {
        var resource = await _resources.GetByIdAsync(resourceId)
            ?? throw new KeyNotFoundException($"Resource {resourceId} not found.");
        resource.IsDeleted = true;
        resource.UpdatedBy = userId;
        resource.UpdatedAt = DateTime.UtcNow;
        await _resources.UpdateAsync(resource);
    }
    /// <summary>PR1 — records which attachment is the signed contract. Replacing it is allowed
    /// (contracts get re-signed); the superseded file stays in the attachment list.</summary>
    public async Task SetContractAttachmentAsync(string projectId, string attachmentId, string userId)
    {
        var project = await _projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var previous = project.ContractAttachmentId;
        project.ContractAttachmentId = attachmentId;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = userId;
        await _projects.UpdateAsync(project);

        await _history.CreateAsync(new ProjectHistory
        {
            ProjectId = projectId,
            Action    = previous is null ? "ContractUploaded" : "ContractReplaced",
            OldValue  = previous,
            NewValue  = attachmentId,
            ChangedByUserId = userId, CreatedBy = userId, UpdatedBy = userId,
        });
    }

}
