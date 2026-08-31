using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Appraisals;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H9 (HR-014 to HR-020, P14–P17) — KPI scorecards and targets, the appraisal workflow, 360 feedback and
/// improvement plans.
/// <para>The four appraisal steps carry the permissions of the people who take them: the employee's own
/// self-assessment is <c>hr.read.own</c>, the line-manager review and 360 requests are <c>hr.manager</c>, MD
/// sign-off is <c>hr.approve</c>, and configuration is <c>hr.write</c>. The service enforces the ORDER and the
/// second-officer rules on top — permissions say who may act, not when.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class AppraisalController(IAppraisalService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("appraisals/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary([FromQuery] int? year)
        => Ok(new { data = await service.GetSummaryAsync(year) });

    // ── Scorecards and targets (P14) ──
    [HttpGet("appraisals/scorecards")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListScorecards([FromQuery] int? year, [FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListScorecardsAsync(year, includeInactive) });

    [HttpGet("appraisals/scorecards/{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetScorecard(string id)
    {
        var card = await service.GetScorecardAsync(id);
        return card is null ? NotFound(new { message = "Scorecard not found." }) : Ok(new { data = card });
    }

    /// <summary>Refuses to save unless the active items' weights total exactly 100% (P14 design note).</summary>
    [HttpPost("appraisals/scorecards")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreateScorecard([FromBody] SaveKpiScorecardDto dto)
        => Act(await service.SaveScorecardAsync(null, dto, UserId));

    [HttpPut("appraisals/scorecards/{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UpdateScorecard(string id, [FromBody] SaveKpiScorecardDto dto)
        => Act(await service.SaveScorecardAsync(id, dto, UserId));

    [HttpPost("appraisals/scorecards/{id}/assign-targets")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AssignTargets(string id)
        => Act(await service.AssignTargetsAsync(id, UserId));

    [HttpGet("appraisals/targets")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListTargets([FromQuery] int? year, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListTargetsAsync(year, employeeId) });

    [HttpPost("appraisals/targets")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SetTarget([FromBody] SetTargetDto dto)
        => Act(await service.SetTargetAsync(dto, UserId));

    // ── Cycles and the workflow (P15) ──
    [HttpGet("appraisals/cycles")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListCycles([FromQuery] int? year)
        => Ok(new { data = await service.ListCyclesAsync(year) });

    /// <summary>Opens the window and raises one appraisal per active employee.</summary>
    [HttpPost("appraisals/cycles")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> OpenCycle([FromBody] OpenCycleDto dto)
        => Act(await service.OpenCycleAsync(dto, Schema, UserId));

    [HttpPost("appraisals/cycles/{id}/close")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CloseCycle(string id)
        => Act(await service.CloseCycleAsync(id, UserId));

    [HttpGet("appraisals")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> List(
        [FromQuery] string? cycleId, [FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListAppraisalsAsync(cycleId, status, employeeId) });

    [HttpGet("appraisals/{id}")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> Get(string id)
    {
        var a = await service.GetAppraisalAsync(id);
        return a is null ? NotFound(new { message = "Appraisal not found." }) : Ok(new { data = a });
    }

    /// <summary>Step 1 — the employee's own assessment.</summary>
    [HttpPost("appraisals/{id}/self-assessment")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> SubmitSelf(string id, [FromBody] SubmitAppraisalStepDto dto)
        => Act(await service.SubmitSelfAsync(id, dto, UserId));

    /// <summary>Step 2 — line-manager review. Their score is the verdict.</summary>
    [HttpPost("appraisals/{id}/review")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> Review(string id, [FromBody] SubmitAppraisalStepDto dto)
        => Act(await service.ReviewAsync(id, dto, UserId, UserName));

    /// <summary>Step 3 — MD sign-off. A score below the threshold raises an improvement plan here.</summary>
    [HttpPost("appraisals/{id}/sign-off")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> SignOff(string id, [FromBody] SubmitAppraisalStepDto dto)
        => Act(await service.SignOffAsync(id, dto, Schema, UserId, UserName));

    /// <summary>Step 4 — HR records it and the appraisal closes.</summary>
    [HttpPost("appraisals/{id}/record")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Record(string id, [FromBody] SubmitAppraisalStepDto dto)
        => Act(await service.RecordAsync(id, dto, UserId, UserName));

    // ── 360 feedback (P16) ──
    /// <summary>Counts and averages only — individual ratings are anonymous to the employee.</summary>
    [HttpGet("appraisals/{id}/feedback-360")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> GetFeedback(string id)
    {
        var summary = await service.GetFeedbackAsync(id);
        return summary is null ? NotFound(new { message = "Appraisal not found." }) : Ok(new { data = summary });
    }

    [HttpPost("appraisals/{id}/feedback-360/request")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> RequestFeedback(string id, [FromBody] Feedback360RequestDto dto)
        => Act(await service.RequestFeedbackAsync(id, dto, Schema, UserId));

    /// <summary>A reviewer submits their own feedback — identified by their employee record, so nobody can
    /// answer on another's behalf.</summary>
    [HttpPost("appraisals/{id}/feedback-360")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> SubmitFeedback(string id, [FromBody] SubmitFeedback360Dto dto)
        => Act(await service.SubmitFeedbackAsync(id, dto, UserId));

    // ── Improvement plans (P17) ──
    [HttpGet("appraisals/pips")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListPips([FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListPipsAsync(status, employeeId) });

    [HttpPut("appraisals/pips/{id}")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> UpdatePip(string id, [FromBody] SavePipDto dto)
        => Act(await service.UpdatePipAsync(id, dto, UserId));

    [HttpPost("appraisals/pips/{id}/close")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> ClosePip(string id, [FromBody] ClosePipDto dto)
        => Act(await service.ClosePipAsync(id, dto, Schema, UserId, UserName));

    private IActionResult Act(AppraisalActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
