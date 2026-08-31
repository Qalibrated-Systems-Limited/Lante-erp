using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Attachments;
using OperationsService.Core.DTOs.Schedule;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// PR1 — project planning: schedule/baseline/dependencies, the detailed approvable budget, the
/// contract rate card, and quote-vs-spend.
///
/// Separate from ProjectsController, which is already large and owns the project lifecycle. These
/// are all "how is the work planned and priced" concerns and read as one surface.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects/{projectId:guid}")]
[Authorize]
public class ProjectPlanningController(
    IProjectScheduleService schedule,
    IProjectBudgetService budgets,
    IProjectService projectService,
    IAttachmentService attachments) : ControllerBase
{
    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    private static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    // ── Schedule ─────────────────────────────────────────────────────────────

    [HttpGet("schedule")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectScheduleDto>>> GetSchedule(Guid projectId)
        => Ok(await schedule.GetScheduleAsync(projectId.ToString()));

    /// <summary>Freezes the current plan as the baseline. Refused if already baselined.</summary>
    [HttpPost("baseline")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<object>>> SetBaseline(Guid projectId, [FromQuery] bool force = false)
    {
        var count = await schedule.SetBaselineAsync(projectId.ToString(), UserId, force);
        return Ok<object>(new { milestonesBaselined = count });
    }

    [HttpPost("milestone-dependencies")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> LinkMilestones(Guid projectId, [FromBody] CreateDependencyDto dto)
    {
        var dep = await schedule.LinkMilestonesAsync(
            projectId.ToString(), dto.PredecessorId, dto.SuccessorId, dto.LagDays, UserId);
        return Ok<object>(new { dep.Id, dep.PredecessorMilestoneId, dep.SuccessorMilestoneId, dep.LagDays });
    }

    [HttpDelete("milestone-dependencies/{dependencyId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<IActionResult> UnlinkMilestones(Guid projectId, Guid dependencyId)
    {
        await schedule.UnlinkMilestonesAsync(dependencyId.ToString());
        return NoContent();
    }

    [HttpGet("task-dependencies")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<DependencyDto>>>> GetTaskDependencies(Guid projectId)
        => Ok(await schedule.GetTaskDependenciesAsync(projectId.ToString()));

    [HttpPost("task-dependencies")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> LinkTasks(Guid projectId, [FromBody] CreateDependencyDto dto)
    {
        var dep = await schedule.LinkTasksAsync(
            projectId.ToString(), dto.PredecessorId, dto.SuccessorId, dto.LagDays, UserId);
        return Ok<object>(new { dep.Id, dep.PredecessorTaskId, dep.SuccessorTaskId, dep.LagDays });
    }

    [HttpDelete("task-dependencies/{dependencyId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<IActionResult> UnlinkTasks(Guid projectId, Guid dependencyId)
    {
        await schedule.UnlinkTasksAsync(dependencyId.ToString());
        return NoContent();
    }

    // ── Detailed budget ──────────────────────────────────────────────────────

    [HttpGet("budget-versions")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<BudgetVersionDto>>>> GetVersions(Guid projectId)
        => Ok(await budgets.GetVersionsAsync(projectId.ToString()));

    [HttpGet("budget-versions/{versionId:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> GetVersion(Guid projectId, Guid versionId)
    {
        var v = await budgets.GetVersionAsync(versionId.ToString());
        return v is null
            ? NotFound(new ApiResponse<BudgetVersionDto> { Success = false, Message = "Budget version not found." })
            : Ok(v);
    }

    [HttpPost("budget-versions")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> CreateVersion(
        Guid projectId, [FromBody] CreateBudgetVersionDto dto)
        => Ok(await budgets.CreateVersionAsync(projectId.ToString(), dto, UserId));

    [HttpPost("budget-versions/{versionId:guid}/lines")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> UpsertLine(
        Guid projectId, Guid versionId, [FromBody] UpsertBudgetLineDto dto)
        => Ok(await budgets.UpsertLineAsync(versionId.ToString(), dto, UserId));

    [HttpDelete("budget-versions/{versionId:guid}/lines/{lineId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> DeleteLine(
        Guid projectId, Guid versionId, Guid lineId)
        => Ok(await budgets.DeleteLineAsync(versionId.ToString(), lineId.ToString(), UserId));

    [HttpPost("budget-versions/{versionId:guid}/submit")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> SubmitBudget(Guid projectId, Guid versionId)
        => Ok(await budgets.SubmitAsync(versionId.ToString(), UserId));

    /// <summary>Approving is what sets the project's baseline budget.</summary>
    [HttpPost("budget-versions/{versionId:guid}/approve")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> ApproveBudget(Guid projectId, Guid versionId)
        => Ok(await budgets.ApproveAsync(versionId.ToString(), UserId));

    [HttpPost("budget-versions/{versionId:guid}/reject")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<BudgetVersionDto>>> RejectBudget(
        Guid projectId, Guid versionId, [FromBody] RejectBudgetDto dto)
        => Ok(await budgets.RejectAsync(versionId.ToString(), dto.Reason, UserId));

    // ── Contract rate card ───────────────────────────────────────────────────

    [HttpGet("contract-rates")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<ContractRateDto>>>> GetRates(
        Guid projectId, [FromQuery] bool activeOnly = false)
        => Ok(await budgets.GetRatesAsync(projectId.ToString(), activeOnly));

    [HttpPost("contract-rates")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ContractRateDto>>> AddRate(
        Guid projectId, [FromBody] UpsertContractRateDto dto)
        => Ok(await budgets.AddRateAsync(projectId.ToString(), dto, UserId));

    [HttpDelete("contract-rates/{rateId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<IActionResult> DeactivateRate(Guid projectId, Guid rateId)
    {
        await budgets.DeactivateRateAsync(rateId.ToString(), UserId);
        return NoContent();
    }

    // ── Contract document ────────────────────────────────────────────────────

    /// <summary>
    /// Uploads the signed contract and stamps it on the project in one call.
    ///
    /// A single call on purpose: uploading and then separately linking leaves a window where the
    /// file exists but the project doesn't know about it, and a failed second step would strand it.
    /// The file itself goes through the normal attachment store, so it is listed, downloaded and
    /// deleted like every other project document.
    /// </summary>
    [HttpPost("contract")]
    [Authorize(Policy = "Permission:projects.write")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<AttachmentReadDto>>> UploadContract(
        Guid projectId, IFormFile file, [FromForm] string? description = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiResponse<AttachmentReadDto> { Success = false, Message = "No file provided." });

        var uploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH")
                         ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, "attachments", storedName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var attachment = await attachments.UploadAsync(
            new UploadAttachmentDto
            {
                EntityType = "Project",
                EntityId   = projectId.ToString(),
                Description = string.IsNullOrWhiteSpace(description) ? "Signed contract" : description,
            },
            file.FileName, file.ContentType, file.Length,
            $"/uploads/attachments/{storedName}", UserId, UserName);

        await projectService.SetContractAttachmentAsync(projectId.ToString(), attachment.Id, UserId);
        return Ok(attachment);
    }

    /// <summary>Every document filed against the project, contract included.</summary>
    [HttpGet("documents")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AttachmentReadDto>>>> GetDocuments(Guid projectId)
        => Ok(await attachments.GetByEntityAsync("Project", projectId.ToString()));

    // ── Quote vs spend ───────────────────────────────────────────────────────

    [HttpGet("commercials")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectCommercialsDto>>> GetCommercials(Guid projectId)
        => Ok(await budgets.GetCommercialsAsync(projectId.ToString()));
}
