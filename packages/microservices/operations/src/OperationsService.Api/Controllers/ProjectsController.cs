using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Approvals;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Milestones;
using OperationsService.Core.DTOs.Projects;
using OperationsService.Core.DTOs.Resources;
using OperationsService.Core.DTOs.Tasks;
using OperationsService.Core.Interfaces.Services;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
[Authorize]
public class ProjectsController(IProjectService projectService, IHseGateway hse) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string DepartmentId => User.FindFirstValue("department_id") ?? string.Empty;
    private List<string> DepartmentIds => (User.FindFirstValue("department_ids") ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    private bool IsAdmin => User.HasClaim("permission", "system.admin");
    private bool IsMd => User.HasClaim(ClaimTypes.Role, "ManagingDirector");
    private bool IsFinance => User.HasClaim(ClaimTypes.Role, "Finance");
    private bool CanReadAllProjects => IsAdmin || User.HasClaim("permission", "projects.read.all")
                                                 || User.HasClaim("permission", "projects.approve");
    private bool CanReadDeptProjects => CanReadAllProjects || User.HasClaim("permission", "projects.read.dept")
                                                            || User.HasClaim("permission", "projects.write")
                                                            || User.HasClaim("permission", "projects.delete")
                                                            || User.HasClaim("permission", "operations.write")
                                                            || User.HasClaim("permission", "operations.approve");

    [HttpGet]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ProjectReadDto>>>> GetAll([FromQuery] ProjectFilterParameters filters)
    {
        if (CanReadAllProjects)
        {
            // no scope restriction
        }
        else if (CanReadDeptProjects)
        {
            var ids = DepartmentIds;
            if (ids.Count > 0)
                filters.DepartmentIds = ids;
            else
                filters.DepartmentId = DepartmentId;
        }
        else
        {
            // projects.read.own only — show only projects where user is manager or a resource
            filters.MemberUserId = UserId;
        }

        var result = await projectService.GetAllAsync(filters, null);
        return Ok(new ApiResponse<PaginatedResult<ProjectReadDto>> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> GetById(Guid id)
    {
        var project = await projectService.GetByIdAsync(id.ToString());
        if (project is null) return NotFound(new ApiResponse<ProjectReadDto> { Success = false, Message = "Project not found." });
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> Create([FromBody] CreateProjectDto dto)
    {
        var project = await projectService.CreateAsync(dto, UserId, DepartmentId);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var project = await projectService.UpdateAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await projectService.DeleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Project deleted." });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> Submit(Guid id)
    {
        var project = await projectService.SubmitForApprovalAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpPost("{id:guid}/approve/md")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<ProjectApprovalReadDto>>> ApproveMd(Guid id, [FromBody] ReviewProjectApprovalDto dto)
    {
        var approval = await projectService.ReviewMdApprovalAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ProjectApprovalReadDto> { Success = true, Data = approval });
    }

    [HttpPost("{id:guid}/approve/finance")]
    [Authorize(Policy = "Permission:finance.approve")]
    public async Task<ActionResult<ApiResponse<ProjectApprovalReadDto>>> ApproveFinance(Guid id, [FromBody] ReviewProjectApprovalDto dto)
    {
        var approval = await projectService.ReviewFinanceApprovalAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ProjectApprovalReadDto> { Success = true, Data = approval });
    }

    // O1 — MD-activation gate: move an approved (Planning) project to Active so work/expenditure can begin.
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> Activate(Guid id)
    {
        var project = await projectService.ActivateAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpPost("{id:guid}/hold")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> PutOnHold(Guid id, [FromBody] string reason)
    {
        var project = await projectService.PutOnHoldAsync(id.ToString(), reason, UserId);
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> Resume(Guid id)
    {
        var project = await projectService.ResumeAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<ProjectReadDto>>> Close(Guid id)
    {
        var project = await projectService.CloseAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<ProjectReadDto> { Success = true, Data = project });
    }

    // ── Milestones ─────────────────────────────────────────────────────────────

    [HttpGet("{projectId:guid}/milestones")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MilestoneReadDto>>>> GetMilestones(Guid projectId)
    {
        var milestones = await projectService.GetMilestonesAsync(projectId.ToString());
        return Ok(new ApiResponse<IEnumerable<MilestoneReadDto>> { Success = true, Data = milestones });
    }

    [HttpGet("{projectId:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ApiResponse<MilestoneReadDto>>> GetMilestone(Guid projectId, Guid milestoneId)
    {
        var m = await projectService.GetMilestoneByIdAsync(milestoneId.ToString());
        if (m is null) return NotFound(new ApiResponse<MilestoneReadDto> { Success = false, Message = "Milestone not found." });
        return Ok(new ApiResponse<MilestoneReadDto> { Success = true, Data = m });
    }

    [HttpPost("{projectId:guid}/milestones")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<MilestoneReadDto>>> CreateMilestone(Guid projectId, [FromBody] CreateMilestoneDto dto)
    {
        var m = await projectService.CreateMilestoneAsync(projectId.ToString(), dto, UserId);
        return CreatedAtAction(nameof(GetMilestone), new { projectId, milestoneId = m.Id }, new ApiResponse<MilestoneReadDto> { Success = true, Data = m });
    }

    [HttpPut("{projectId:guid}/milestones/{milestoneId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<MilestoneReadDto>>> UpdateMilestone(Guid projectId, Guid milestoneId, [FromBody] UpdateMilestoneDto dto)
    {
        var m = await projectService.UpdateMilestoneAsync(milestoneId.ToString(), dto, UserId);
        return Ok(new ApiResponse<MilestoneReadDto> { Success = true, Data = m });
    }

    // O1 — client sign-off on a milestone (completes it + captures who/when; O3 invoicing hook).
    [HttpPost("{projectId:guid}/milestones/{milestoneId:guid}/sign-off")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<MilestoneReadDto>>> SignOffMilestone(Guid projectId, Guid milestoneId, [FromBody] SignOffMilestoneDto dto)
    {
        var m = await projectService.SignOffMilestoneAsync(milestoneId.ToString(), dto, UserId);
        return Ok(new ApiResponse<MilestoneReadDto> { Success = true, Data = m });
    }

    // O3 — daily milestone progress update (logs to MILESTONE_UPDATE_LOG; satisfies the 5PM requirement).
    [HttpPost("{projectId:guid}/milestones/{milestoneId:guid}/updates")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<MilestoneUpdateLogReadDto>>> AddMilestoneUpdate(Guid projectId, Guid milestoneId, [FromBody] AddMilestoneUpdateDto dto)
    {
        var log = await projectService.AddMilestoneUpdateAsync(milestoneId.ToString(), dto, UserId);
        return Ok(new ApiResponse<MilestoneUpdateLogReadDto> { Success = true, Data = log });
    }

    [HttpGet("{projectId:guid}/milestones/{milestoneId:guid}/updates")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MilestoneUpdateLogReadDto>>>> GetMilestoneUpdates(Guid projectId, Guid milestoneId)
    {
        var logs = await projectService.GetMilestoneUpdatesAsync(milestoneId.ToString());
        return Ok(new ApiResponse<IEnumerable<MilestoneUpdateLogReadDto>> { Success = true, Data = logs });
    }

    [HttpDelete("{projectId:guid}/milestones/{milestoneId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMilestone(Guid projectId, Guid milestoneId)
    {
        await projectService.DeleteMilestoneAsync(milestoneId.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Milestone deleted." });
    }

    // ── Tasks ──────────────────────────────────────────────────────────────────

    [HttpGet("{projectId:guid}/milestones/{milestoneId:guid}/tasks")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TaskReadDto>>>> GetTasks(Guid projectId, Guid milestoneId)
    {
        var tasks = await projectService.GetTasksAsync(milestoneId.ToString());
        return Ok(new ApiResponse<IEnumerable<TaskReadDto>> { Success = true, Data = tasks });
    }

    [HttpPost("{projectId:guid}/milestones/{milestoneId:guid}/tasks")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<TaskReadDto>>> CreateTask(Guid projectId, Guid milestoneId, [FromBody] CreateTaskDto dto)
    {
        var task = await projectService.CreateTaskAsync(milestoneId.ToString(), dto, UserId);
        return Ok(new ApiResponse<TaskReadDto> { Success = true, Data = task });
    }

    [HttpPut("{projectId:guid}/milestones/{milestoneId:guid}/tasks/{taskId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<TaskReadDto>>> UpdateTask(Guid projectId, Guid milestoneId, Guid taskId, [FromBody] UpdateTaskDto dto)
    {
        var task = await projectService.UpdateTaskAsync(taskId.ToString(), dto, UserId);
        return Ok(new ApiResponse<TaskReadDto> { Success = true, Data = task });
    }

    [HttpPost("{projectId:guid}/milestones/{milestoneId:guid}/tasks/{taskId:guid}/dispatch")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<TaskReadDto>>> DispatchTask(Guid projectId, Guid milestoneId, Guid taskId, [FromBody] DispatchTaskDto dto)
    {
        var task = await projectService.DispatchTaskAsync(taskId.ToString(), dto, UserId, DepartmentId);
        return Ok(new ApiResponse<TaskReadDto> { Success = true, Data = task });
    }

    [HttpDelete("{projectId:guid}/milestones/{milestoneId:guid}/tasks/{taskId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTask(Guid projectId, Guid milestoneId, Guid taskId)
    {
        await projectService.DeleteTaskAsync(taskId.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Task deleted." });
    }

    // ── Budget ─────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/budget")]
    public async Task<ActionResult<ApiResponse<BudgetSummaryDto>>> GetBudget(Guid id)
    {
        var summary = await projectService.GetBudgetSummaryAsync(id.ToString());
        return Ok(new ApiResponse<BudgetSummaryDto> { Success = true, Data = summary });
    }

    [HttpPost("{id:guid}/budget/lines")]
    [Authorize(Policy = "Permission:finance.write")]
    public async Task<ActionResult<ApiResponse<BudgetLineReadDto>>> CreateBudgetLine(Guid id, [FromBody] CreateBudgetLineDto dto)
    {
        dto.ProjectId = id.ToString();
        var line = await projectService.CreateBudgetLineAsync(dto, UserId);
        return Ok(new ApiResponse<BudgetLineReadDto> { Success = true, Data = line });
    }

    [HttpPut("{id:guid}/budget/lines/{lineId:guid}")]
    [Authorize(Policy = "Permission:finance.write")]
    public async Task<ActionResult<ApiResponse<BudgetLineReadDto>>> UpdateBudgetLine(Guid id, Guid lineId, [FromBody] UpdateBudgetLineDto dto)
    {
        var line = await projectService.UpdateBudgetLineAsync(lineId.ToString(), dto, UserId);
        return Ok(new ApiResponse<BudgetLineReadDto> { Success = true, Data = line });
    }

    [HttpDelete("{id:guid}/budget/lines/{lineId:guid}")]
    [Authorize(Policy = "Permission:finance.write")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBudgetLine(Guid id, Guid lineId)
    {
        await projectService.DeleteBudgetLineAsync(lineId.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Budget line deleted." });
    }

    [HttpPost("{id:guid}/budget/costs")]
    [Authorize(Policy = "Permission:finance.write")]
    public async Task<ActionResult<ApiResponse<CostEntryReadDto>>> AddCostEntry(Guid id, [FromBody] CreateCostEntryDto dto)
    {
        dto.ProjectId = id.ToString();
        var entry = await projectService.AddCostEntryAsync(dto, UserId);
        return Ok(new ApiResponse<CostEntryReadDto> { Success = true, Data = entry });
    }

    // O2 — budget-burn alert history (80/90/95/100% threshold crossings).
    [HttpGet("{id:guid}/budget/alerts")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProjectAlertLogReadDto>>>> GetBudgetAlerts(Guid id)
    {
        var alerts = await projectService.GetProjectAlertsAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<ProjectAlertLogReadDto>> { Success = true, Data = alerts });
    }

    // Project audit trail (lifecycle transitions).
    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProjectHistoryReadDto>>>> GetHistory(Guid id)
    {
        var history = await projectService.GetProjectHistoryAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<ProjectHistoryReadDto>> { Success = true, Data = history });
    }

    // Daily site reports (work done / issues / plan for tomorrow).
    [HttpGet("{id:guid}/daily-reports")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProjectDailyReportReadDto>>>> GetDailyReports(Guid id)
    {
        var reports = await projectService.GetDailyReportsAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<ProjectDailyReportReadDto>> { Success = true, Data = reports });
    }

    [HttpPost("{id:guid}/daily-reports")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectDailyReportReadDto>>> AddDailyReport(Guid id, [FromBody] CreateProjectDailyReportDto dto)
    {
        var report = await projectService.AddDailyReportAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ProjectDailyReportReadDto> { Success = true, Data = report });
    }

    // O8 — project HSE summary (read-only seam to hse-service; clear until the seam is enabled).
    [HttpGet("{id:guid}/hse-summary")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<HseProjectSummary>>> GetHseSummary(Guid id)
    {
        var summary = await hse.GetProjectHseSummaryAsync(id.ToString());
        return Ok(new ApiResponse<HseProjectSummary> { Success = true, Data = summary });
    }

    // ── Resources ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/resources")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProjectResourceReadDto>>>> GetResources(Guid id)
    {
        var resources = await projectService.GetResourcesAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<ProjectResourceReadDto>> { Success = true, Data = resources });
    }

    [HttpPost("{id:guid}/resources")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectResourceReadDto>>> AddResource(Guid id, [FromBody] AddProjectResourceDto dto)
    {
        dto.ProjectId = id.ToString();
        var resource = await projectService.AddResourceAsync(dto, UserId);
        return Ok(new ApiResponse<ProjectResourceReadDto> { Success = true, Data = resource });
    }

    [HttpPut("{id:guid}/resources/{resourceId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectResourceReadDto>>> UpdateResource(Guid id, Guid resourceId, [FromBody] UpdateProjectResourceDto dto)
    {
        var resource = await projectService.UpdateResourceAsync(resourceId.ToString(), dto, UserId);
        return Ok(new ApiResponse<ProjectResourceReadDto> { Success = true, Data = resource });
    }

    [HttpDelete("{id:guid}/resources/{resourceId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveResource(Guid id, Guid resourceId)
    {
        await projectService.RemoveResourceAsync(resourceId.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Resource removed." });
    }
}
