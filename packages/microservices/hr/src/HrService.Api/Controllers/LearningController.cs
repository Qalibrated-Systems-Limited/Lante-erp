using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Learning;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H7 (HR-025 to HR-036, P22 + P23 + P25 + P26) — learning and development: plans, the training hours ledger,
/// mandatory-training compliance, knowledge sharing and the L&amp;D budget.
/// <para>This sits on the general <c>hr.*</c> tier, not the ring-fenced payroll one: training records are not
/// pay data, and line managers need to see their team's. Approving a plan is <c>hr.manager</c>, since the line
/// manager is the approver; changing the mandatory-training matrix is <c>hr.write</c>, because it decides who
/// can be given a rise.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class LearningController(ILearningService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("learning/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary([FromQuery] int? year, CancellationToken ct)
        => Ok(new { data = await service.GetSummaryAsync(year, ct) });

    // ── Learning & development plans (P22) ──
    [HttpGet("learning/plans")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> ListPlans(
        [FromQuery] int? year, [FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListPlansAsync(year, status, employeeId) });

    [HttpGet("learning/plans/{id}")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> GetPlan(string id)
    {
        var plan = await service.GetPlanAsync(id);
        return plan is null ? NotFound(new { message = "Plan not found." }) : Ok(new { data = plan });
    }

    [HttpPost("learning/plans")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SavePlan([FromBody] SaveLdpDto dto)
        => Act(await service.SavePlanAsync(dto, UserId));

    [HttpPost("learning/plans/{id}/submit")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SubmitPlan(string id)
        => Act(await service.SubmitPlanAsync(id, UserId));

    /// <summary>Line-manager decision. The author cannot approve their own plan.</summary>
    [HttpPost("learning/plans/{id}/decide")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> DecidePlan(string id, [FromBody] DecideLdpDto dto)
        => Act(await service.DecidePlanAsync(id, dto, UserId, UserName));

    // ── Training events and hours (P23) ──
    [HttpGet("learning/training")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListTraining(
        [FromQuery] int? year, [FromQuery] string? departmentId, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListTrainingAsync(year, departmentId, employeeId) });

    /// <summary>Logs an event and its attendance together — hours, LDP objectives and budget in one step.</summary>
    [HttpPost("learning/training")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> LogTraining([FromBody] SaveTrainingEventDto dto)
        => Act(await service.LogTrainingAsync(dto, Schema, UserId));

    [HttpGet("learning/hours")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListHours([FromQuery] int? year, [FromQuery] string? departmentId)
        => Ok(new { data = await service.ListHoursAsync(year, departmentId) });

    // ── Mandatory training (P23, HR-029/034) ──
    [HttpGet("learning/requirements")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListRequirements([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListRequirementsAsync(includeInactive) });

    [HttpPost("learning/requirements/seed")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SeedRequirements() => Act(await service.SeedRequirementsAsync(UserId));

    [HttpPost("learning/requirements")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreateRequirement([FromBody] SaveMandatoryRequirementDto dto)
        => Act(await service.SaveRequirementAsync(null, dto, UserId));

    [HttpPut("learning/requirements/{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UpdateRequirement(string id, [FromBody] SaveMandatoryRequirementDto dto)
        => Act(await service.SaveRequirementAsync(id, dto, UserId));

    /// <summary>Compliance across the company, with HSE and anti-bribery evidence read live from the services
    /// that own it. A source that cannot be read reports as unknown, never as a lapse.</summary>
    [HttpGet("learning/compliance")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Compliance([FromQuery] string? employeeId, CancellationToken ct)
        => Ok(new { data = await service.GetComplianceAsync(employeeId, ct) });

    /// <summary>The H8 gate — may this employee be proposed for a salary increment (HR-029/HR-035)?</summary>
    [HttpGet("learning/increment-eligibility")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> IncrementEligibility([FromQuery] string employeeId, CancellationToken ct)
    {
        var result = await service.GetIncrementEligibilityAsync(employeeId, ct);
        return result is null ? NotFound(new { message = "Employee not found." }) : Ok(new { data = result });
    }

    // ── Knowledge sharing (P25) ──
    [HttpGet("learning/knowledge-sharing")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> ListSessions([FromQuery] int? year, [FromQuery] int? month)
        => Ok(new { data = await service.ListSessionsAsync(year, month) });

    [HttpPost("learning/knowledge-sharing")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> LogSession([FromBody] SaveKnowledgeSharingDto dto)
        => Act(await service.LogSessionAsync(dto, UserId));

    // ── L&D budgets (P26) ──
    [HttpGet("learning/budgets")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListBudgets([FromQuery] int? year)
        => Ok(new { data = await service.ListBudgetsAsync(year) });

    [HttpPost("learning/budgets")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SaveBudget([FromBody] SaveLdBudgetDto dto)
        => Act(await service.SaveBudgetAsync(dto, UserId));

    /// <summary>Runs the L&amp;D red-flag sweep now instead of waiting for the daily tick. Idempotent.</summary>
    [HttpPost("learning/sweep")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Sweep(CancellationToken ct)
        => Ok(new { data = await service.RunLearningSweepAsync(Schema, UserId, ct) });

    private IActionResult Act(LearningActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
