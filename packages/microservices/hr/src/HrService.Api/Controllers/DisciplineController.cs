using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Discipline;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H10 (HR-021 to HR-024, P18–P21) — disciplinary cases, warnings, grievances and separation.
/// <para>Case handling is <c>hr.write</c>; outcomes, appeals and separation approval need <c>hr.approve</c>,
/// because they are the decisions that end somebody's employment or stand up in a tribunal. Raising a
/// grievance and acknowledging a warning are <c>hr.read.own</c> — they are the employee's own acts, and the
/// service additionally refuses to let anyone acknowledge a warning on another person's behalf.</para>
/// <para>Final dues sit behind the ring-fenced <c>hr.payroll.*</c> tier: it is pay data.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class DisciplineController(IDisciplineService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("discipline/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── Disciplinary cases (P18) ──
    [HttpGet("discipline/cases")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListCases([FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListCasesAsync(status, employeeId) });

    [HttpGet("discipline/cases/{id}")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> GetCase(string id)
    {
        var c = await service.GetCaseAsync(id);
        return c is null ? NotFound(new { message = "Case not found." }) : Ok(new { data = c });
    }

    [HttpPost("discipline/cases")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> OpenCase([FromBody] OpenCaseDto dto)
        => Act(await service.OpenCaseAsync(dto, UserId));

    [HttpPost("discipline/cases/{id}/show-cause")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> IssueShowCause(string id, [FromBody] IssueShowCauseDto? dto)
        => Act(await service.IssueShowCauseAsync(id, dto ?? new IssueShowCauseDto(), Schema, UserId));

    [HttpPost("discipline/cases/{id}/response")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RecordResponse(string id, [FromBody] RecordResponseDto dto)
        => Act(await service.RecordResponseAsync(id, dto, UserId));

    /// <summary>The hearing outcome — the decision that may end someone's employment.</summary>
    [HttpPost("discipline/cases/{id}/outcome")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> RecordOutcome(string id, [FromBody] RecordOutcomeDto dto)
        => Act(await service.RecordOutcomeAsync(id, dto, Schema, UserId, UserName));

    /// <summary>Lodge an appeal (no decision) or decide one. Whoever recorded the outcome cannot decide it.</summary>
    [HttpPost("discipline/cases/{id}/appeal")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> RecordAppeal(string id, [FromBody] RecordAppealDto dto)
        => Act(await service.RecordAppealAsync(id, dto, UserId, UserName));

    [HttpPost("discipline/cases/{id}/close")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CloseCase(string id, [FromBody] RecordAppealDto? dto)
        => Act(await service.CloseCaseAsync(id, dto?.Decision, UserId));

    // ── Warnings (P19) ──
    [HttpGet("discipline/warnings")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> ListWarnings([FromQuery] string? employeeId, [FromQuery] bool includeExpired = false)
        => Ok(new { data = await service.ListWarningsAsync(employeeId, includeExpired) });

    [HttpPost("discipline/warnings")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> IssueWarning([FromBody] IssueWarningDto dto)
        => Act(await service.IssueWarningAsync(dto, Schema, UserId));

    /// <summary>The employee's own acknowledgement — HR cannot record it for them.</summary>
    [HttpPost("discipline/warnings/{id}/acknowledge")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> AcknowledgeWarning(string id, [FromBody] AcknowledgeWarningDto? dto)
        => Act(await service.AcknowledgeWarningAsync(id, dto ?? new AcknowledgeWarningDto(), UserId));

    // ── Grievances (P20) ──
    [HttpGet("discipline/grievances")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListGrievances([FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListGrievancesAsync(status, employeeId) });

    [HttpPost("discipline/grievances")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> SubmitGrievance([FromBody] SubmitGrievanceDto dto)
        => Act(await service.SubmitGrievanceAsync(dto, UserId));

    [HttpPost("discipline/grievances/{id}/acknowledge")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AcknowledgeGrievance(string id)
        => Act(await service.AcknowledgeGrievanceAsync(id, UserId, UserName));

    [HttpPost("discipline/grievances/{id}/assign")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AssignGrievance(string id, [FromBody] AssignGrievanceDto dto)
        => Act(await service.AssignGrievanceAsync(id, dto, UserId));

    [HttpPost("discipline/grievances/{id}/resolve")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> ResolveGrievance(string id, [FromBody] ResolveGrievanceDto dto)
        => Act(await service.ResolveGrievanceAsync(id, dto, Schema, UserId, UserName));

    // ── Separation and final dues (P21) — pay data, so the payroll tier ──
    [HttpGet("discipline/separations")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListSeparations([FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListSeparationsAsync(status, employeeId) });

    [HttpGet("discipline/separations/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> GetSeparation(string id)
    {
        var s = await service.GetSeparationAsync(id);
        return s is null ? NotFound(new { message = "Separation not found." }) : Ok(new { data = s });
    }

    [HttpPost("discipline/separations")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> InitiateSeparation([FromBody] InitiateSeparationDto dto, CancellationToken ct)
        => Act(await service.InitiateSeparationAsync(dto, UserId, ct));

    [HttpPut("discipline/separations/{id}/dues")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> AdjustDues(string id, [FromBody] AdjustDuesDto dto)
        => Act(await service.AdjustDuesAsync(id, dto, UserId));

    [HttpPost("discipline/separations/{id}/submit")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SubmitSeparation(string id)
        => Act(await service.SubmitSeparationAsync(id, UserId));

    /// <summary>MD approval of the final dues. The person who prepared them cannot approve them.</summary>
    [HttpPost("discipline/separations/{id}/decide")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> DecideSeparation(string id, [FromBody] DecideSeparationDto dto)
        => Act(await service.DecideSeparationAsync(id, dto, UserId, UserName));

    /// <summary>Records payment — and only now does the employee leave the payroll (P21 step 21.5).</summary>
    [HttpPost("discipline/separations/{id}/pay")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> PaySeparation(string id, [FromBody] PaySeparationDto? dto)
        => Act(await service.PaySeparationAsync(id, dto ?? new PaySeparationDto(), Schema, UserId, UserName));

    /// <summary>Runs the H10 sweep now instead of waiting for the daily tick. Idempotent.</summary>
    [HttpPost("discipline/sweep")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Sweep()
        => Ok(new { data = await service.RunDisciplineSweepAsync(Schema, UserId) });

    private IActionResult Act(DisciplineActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
