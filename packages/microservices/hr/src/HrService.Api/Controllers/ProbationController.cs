using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Probation;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>H2 (HR-003/HR-006, P3 + P33) — probation milestones and fixed-term contract renewal. The daily
/// sweep raises the reviews and expiry warnings; HR, the line manager and the MD record the outcomes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class ProbationController(IProbationService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("probation/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── Probation reviews (P3) ──
    [HttpGet("probation/reviews")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListReviews([FromQuery] string? outcome, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListReviewsAsync(outcome, employeeId) });

    /// <summary>Records confirmed / extended / terminated. Confirmation is the MD's call at the 6-month mark,
    /// so this needs approval rights rather than plain write.</summary>
    [HttpPost("probation/reviews/{reviewId}/outcome")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> RecordOutcome(string reviewId, [FromBody] RecordProbationOutcomeDto dto)
        => Act(await service.RecordOutcomeAsync(reviewId, dto, UserId, UserName));

    // ── Contract renewal (P33) ──
    [HttpGet("contracts/alerts")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListAlerts([FromQuery] string? outcome)
        => Ok(new { data = await service.ListContractAlertsAsync(outcome) });

    [HttpPost("contracts/alerts/{alertId}/renew")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> Renew(string alertId, [FromBody] RenewContractDto dto)
        => Act(await service.RenewContractAsync(alertId, dto, UserId));

    [HttpPost("contracts/alerts/{alertId}/convert-to-permanent")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> Convert(string alertId, [FromBody] ConvertToPermanentDto? dto)
        => Act(await service.ConvertToPermanentAsync(alertId, dto ?? new ConvertToPermanentDto(), UserId));

    [HttpPost("contracts/alerts/{alertId}/let-expire")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> LetExpire(string alertId, [FromBody] LetContractExpireDto? dto)
        => Act(await service.LetContractExpireAsync(alertId, dto ?? new LetContractExpireDto(), UserId));

    /// <summary>Runs the milestone sweep now instead of waiting for the daily tick. Idempotent.</summary>
    [HttpPost("milestones/sweep")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Sweep()
        => Ok(new { data = await service.RunMilestoneSweepAsync(Schema, UserId) });

    private IActionResult Act(ProbationActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
