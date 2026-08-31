using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Governance;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// PR3 — project governance: the RAID risk register and issue log, and the change requests that are
/// the only sanctioned way to move an approved baseline.
///
/// Deciding a change request sits behind the approval permission, not plain write — it re-baselines
/// schedule and budget, which is the same authority as approving the budget itself.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects/{projectId:guid}")]
[Authorize]
public class ProjectGovernanceController(IProjectGovernanceService governance) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    // ── Summary ──────────────────────────────────────────────────────────────

    [HttpGet("governance/summary")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<GovernanceSummaryDto>>> Summary(Guid projectId)
        => Ok(await governance.GetSummaryAsync(projectId.ToString()));

    // ── Risks ────────────────────────────────────────────────────────────────

    [HttpGet("risks")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<ProjectRiskDto>>>> GetRisks(
        Guid projectId, [FromQuery] bool includeClosed = false)
        => Ok(await governance.GetRisksAsync(projectId.ToString(), includeClosed));

    [HttpPost("risks")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectRiskDto>>> AddRisk(
        Guid projectId, [FromBody] UpsertProjectRiskDto dto)
        => Ok(await governance.AddRiskAsync(projectId.ToString(), dto, UserId));

    [HttpPut("risks/{riskId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectRiskDto>>> UpdateRisk(
        Guid riskId, [FromBody] UpsertProjectRiskDto dto)
        => Ok(await governance.UpdateRiskAsync(riskId.ToString(), dto, UserId));

    [HttpDelete("risks/{riskId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<IActionResult> DeleteRisk(Guid riskId)
    {
        await governance.DeleteRiskAsync(riskId.ToString(), UserId);
        return NoContent();
    }

    /// <summary>The risk happened — close it as Realised and open the issue it became.</summary>
    [HttpPost("risks/{riskId:guid}/realise")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectIssueDto>>> RealiseRisk(
        Guid riskId, [FromBody] RealiseRiskDto dto)
        => Ok(await governance.RealiseRiskAsync(riskId.ToString(), dto, UserId));

    // ── Issues ───────────────────────────────────────────────────────────────

    [HttpGet("issues")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<ProjectIssueDto>>>> GetIssues(
        Guid projectId, [FromQuery] bool includeClosed = false)
        => Ok(await governance.GetIssuesAsync(projectId.ToString(), includeClosed));

    [HttpPost("issues")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectIssueDto>>> AddIssue(
        Guid projectId, [FromBody] UpsertProjectIssueDto dto)
        => Ok(await governance.AddIssueAsync(projectId.ToString(), dto, UserId));

    [HttpPut("issues/{issueId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectIssueDto>>> UpdateIssue(
        Guid issueId, [FromBody] UpsertProjectIssueDto dto)
        => Ok(await governance.UpdateIssueAsync(issueId.ToString(), dto, UserId));

    [HttpPost("issues/{issueId:guid}/resolve")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ProjectIssueDto>>> ResolveIssue(
        Guid issueId, [FromBody] ResolveIssueDto dto)
        => Ok(await governance.ResolveIssueAsync(issueId.ToString(), dto, UserId));

    // ── Change requests ──────────────────────────────────────────────────────

    [HttpGet("change-requests")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<ChangeRequestDto>>>> GetChangeRequests(Guid projectId)
        => Ok(await governance.GetChangeRequestsAsync(projectId.ToString()));

    [HttpPost("change-requests")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> CreateChangeRequest(
        Guid projectId, [FromBody] UpsertChangeRequestDto dto)
        => Ok(await governance.CreateChangeRequestAsync(projectId.ToString(), dto, UserId));

    [HttpPut("change-requests/{crId:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> UpdateChangeRequest(
        Guid crId, [FromBody] UpsertChangeRequestDto dto)
        => Ok(await governance.UpdateChangeRequestAsync(crId.ToString(), dto, UserId));

    [HttpPost("change-requests/{crId:guid}/submit")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> SubmitChangeRequest(Guid crId)
        => Ok(await governance.SubmitChangeRequestAsync(crId.ToString(), UserId));

    [HttpPost("change-requests/{crId:guid}/withdraw")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> WithdrawChangeRequest(Guid crId)
        => Ok(await governance.WithdrawChangeRequestAsync(crId.ToString(), UserId));

    /// <summary>Approving re-baselines schedule and budget — hence the approval permission.</summary>
    [HttpPost("change-requests/{crId:guid}/decide")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> DecideChangeRequest(
        Guid crId, [FromBody] DecideChangeRequestDto dto)
        => Ok(await governance.DecideChangeRequestAsync(crId.ToString(), dto, UserId));
}
