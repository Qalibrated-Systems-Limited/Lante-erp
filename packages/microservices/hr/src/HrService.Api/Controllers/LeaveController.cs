using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Leave;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>H3 (HR-005, P4 + P5 + P6) — leave types and entitlements, applications with their tiered approval
/// chain, and the year-end carry-forward cycle.
/// <para>Approvals sit behind <c>hr.manager</c> rather than <c>hr.approve</c>: the first tier of every chain is
/// the line manager, and the service records WHICH tier each decision satisfied. Configuration (leave types,
/// entitlement assignment and adjustment) needs full <c>hr.write</c>, since those change everyone's
/// allowances.</para></summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class LeaveController(ILeaveService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("leave/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── Leave types (P4 step 4.1) ──
    [HttpGet("leave/types")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> ListTypes([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListTypesAsync(includeInactive) });

    [HttpPost("leave/types")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreateType([FromBody] SaveLeaveTypeDto dto)
        => Act(await service.CreateTypeAsync(dto, UserId));

    [HttpPut("leave/types/{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UpdateType(string id, [FromBody] SaveLeaveTypeDto dto)
        => Act(await service.UpdateTypeAsync(id, dto, UserId));

    /// <summary>Installs the QSL policy defaults for a tenant that has none. Idempotent.</summary>
    [HttpPost("leave/types/seed-defaults")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SeedTypes() => Act(await service.SeedDefaultTypesAsync(UserId));

    // ── Entitlements (P4 step 4.2) ──
    [HttpGet("leave/entitlements")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListEntitlements(
        [FromQuery] string? employeeId, [FromQuery] int? year, [FromQuery] string? leaveTypeId)
        => Ok(new { data = await service.ListEntitlementsAsync(employeeId, year, leaveTypeId) });

    [HttpPost("leave/entitlements/assign")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AssignEntitlements([FromQuery] int? year, [FromQuery] string? employeeId)
        => Act(await service.AssignEntitlementsAsync(year ?? DateTime.UtcNow.Year, employeeId, UserId));

    [HttpPost("leave/entitlements/{id}/adjust")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AdjustEntitlement(string id, [FromBody] AdjustEntitlementDto dto)
        => Act(await service.AdjustEntitlementAsync(id, dto, UserId));

    // ── Requests (P5) ──
    [HttpGet("leave/requests")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListRequests(
        [FromQuery] string? status, [FromQuery] string? employeeId, [FromQuery] string? awaitingRole)
        => Ok(new { data = await service.ListRequestsAsync(status, employeeId, awaitingRole) });

    [HttpGet("leave/requests/{id}")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> GetRequest(string id)
    {
        var r = await service.GetRequestAsync(id);
        return r is null ? NotFound(new { message = "Leave request not found." }) : Ok(new { data = r });
    }

    /// <summary>Day count, balance check, document rule and chain for a request that has not been filed yet.</summary>
    [HttpPost("leave/requests/preview")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> Preview([FromBody] CreateLeaveRequestDto dto)
        => Ok(new { data = await service.PreviewAsync(dto) });

    [HttpPost("leave/requests")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateLeaveRequestDto dto)
        => Act(await service.CreateRequestAsync(dto, Schema, UserId, UserName));

    /// <summary>Records the decision for whichever chain step the request is currently sitting on.</summary>
    [HttpPost("leave/requests/{id}/decide")]
    [Authorize(Policy = "Permission:hr.manager")]
    public async Task<IActionResult> Decide(string id, [FromBody] DecideLeaveDto dto)
        => Act(await service.DecideAsync(id, dto, Schema, UserId, UserName));

    [HttpPost("leave/requests/{id}/cancel")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelLeaveDto? dto)
        => Act(await service.CancelAsync(id, dto ?? new CancelLeaveDto(), UserId));

    [HttpPost("leave/requests/{id}/documents")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> AttachDocument(string id, [FromBody] AttachLeaveDocumentDto dto)
        => Act(await service.AttachDocumentAsync(id, dto, UserId));

    // ── Carry-forward (P4 steps 4.3–4.5 / P6) ──
    [HttpGet("leave/carry-forward")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListCarryForward([FromQuery] int? toYear, [FromQuery] string? status)
        => Ok(new { data = await service.ListCarryForwardAsync(toYear, status) });

    /// <summary>Runs the leave sweep now instead of waiting for the daily tick. Idempotent.</summary>
    [HttpPost("leave/sweep")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Sweep()
        => Ok(new { data = await service.RunLeaveSweepAsync(Schema, UserId) });

    private IActionResult Act(LeaveActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
