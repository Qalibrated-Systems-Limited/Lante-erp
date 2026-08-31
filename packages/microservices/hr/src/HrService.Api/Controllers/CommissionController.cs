using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Commission;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H11 (COM-001 to COM-009, P29–P31) — sales commission.
/// <para>Commission is pay, so this sits on the ring-fenced <c>hr.payroll.*</c> tier. Approving a plan or a
/// statement needs <c>hr.payroll.approve</c> — COM-002 makes MD approval of the basis mandatory, and the
/// service additionally refuses to let the preparer approve their own.</para>
/// <para>Raising a dispute is <c>hr.read.own</c>: it is the employee's own act.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class CommissionController(ICommissionService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("commission/summary")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> Summary([FromQuery] int? year, CancellationToken ct)
        => Ok(new { data = await service.GetSummaryAsync(year, ct) });

    // ── Bands ──
    [HttpGet("commission/bands")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListBands([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListBandsAsync(includeInactive) });

    [HttpPost("commission/bands/seed")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SeedBands() => Act(await service.SeedBandsAsync(UserId));

    [HttpPost("commission/bands")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreateBand([FromBody] SaveCommissionBandDto dto)
        => Act(await service.SaveBandAsync(null, dto, UserId));

    [HttpPut("commission/bands/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdateBand(string id, [FromBody] SaveCommissionBandDto dto)
        => Act(await service.SaveBandAsync(id, dto, UserId));

    // ── Plans ──
    /// <summary>Plans with target and attainment read live from CRM — HR stores neither.</summary>
    [HttpGet("commission/plans")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListPlans([FromQuery] int? year, [FromQuery] string? status, CancellationToken ct)
        => Ok(new { data = await service.ListPlansAsync(year, status, ct) });

    [HttpPost("commission/plans")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SavePlan([FromBody] SaveCommissionPlanDto dto, CancellationToken ct)
        => Act(await service.SavePlanAsync(dto, UserId, ct));

    [HttpPost("commission/plans/{id}/submit")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SubmitPlan(string id) => Act(await service.SubmitPlanAsync(id, UserId));

    /// <summary>MD approval of the commission basis (COM-002).</summary>
    [HttpPost("commission/plans/{id}/decide")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> DecidePlan(string id, [FromBody] DecideCommissionPlanDto dto)
        => Act(await service.DecidePlanAsync(id, dto, UserId, UserName));

    // ── Statements ──
    [HttpGet("commission/statements")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListStatements(
        [FromQuery] int? year, [FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await service.ListStatementsAsync(year, status, employeeId) });

    /// <summary>Computes a quarter's statements from CRM. Refuses if CRM cannot be read.</summary>
    [HttpPost("commission/statements/compute")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> ComputeStatements([FromBody] ComputeStatementsDto? dto, CancellationToken ct)
        => Act(await service.ComputeStatementsAsync(dto ?? new ComputeStatementsDto(), Schema, UserId, ct));

    [HttpPost("commission/statements/{id}/decide")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> DecideStatement(string id, [FromBody] DecideStatementDto dto)
        => Act(await service.DecideStatementAsync(id, dto, UserId, UserName));

    /// <summary>Runs the COM-004 quarterly issue on demand — the same pass the daily worker makes.</summary>
    [HttpPost("commission/sweep")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> Sweep(CancellationToken ct)
        => Ok(new { data = await service.RunCommissionSweepAsync(Schema ?? string.Empty, UserId, ct) });

    // ── Disputes ──
    [HttpGet("commission/disputes")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListDisputes([FromQuery] string? status)
        => Ok(new { data = await service.ListDisputesAsync(status) });

    /// <summary>The employee's own act — raising one freezes the payment and routes to HR (COM-006).</summary>
    [HttpPost("commission/statements/{id}/dispute")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> RaiseDispute(string id, [FromBody] RaiseDisputeDto dto)
        => Act(await service.RaiseDisputeAsync(id, dto, Schema, UserId));

    [HttpPost("commission/disputes/{id}/resolve")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> ResolveDispute(string id, [FromBody] ResolveDisputeDto dto)
        => Act(await service.ResolveDisputeAsync(id, dto, UserId, UserName));

    private IActionResult Act(CommissionActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
