using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Matching;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P6 (DEC-4) — 3-way match &amp; payment handoff. Compares an issued LPO against the goods
/// received (Stores GRN, pushed in by the P5 callback) and the supplier invoice Finance recorded against the
/// LPO. A clean match hands a validated payment voucher to Finance; a failed check raises a matching
/// exception that blocks payment until a Finance Manager resolves it.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/matches")]
[Authorize]
public class MatchesController(IThreeWayMatchService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] MatchFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize, pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    [HttpGet("exceptions")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Exceptions([FromQuery] string? status)
        => Ok(new { data = await service.GetExceptionsAsync(status) });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var m = await service.GetByIdAsync(id);
        return m is null ? NotFound(new { message = "Match not found." }) : Ok(new { data = m });
    }

    [HttpGet("by-po/{poId}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetByPo(string poId)
    {
        var m = await service.GetByPoAsync(poId);
        return m is null ? NotFound(new { message = "This LPO has not been matched yet." }) : Ok(new { data = m });
    }

    [HttpPost("run/{poId}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Run(string poId) => Act(await service.RunAsync(poId, UserId));

    [HttpPost("run-pending")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> RunPending() => Act(await service.RunPendingAsync(UserId));

    [HttpPost("exceptions/{id}/resolve")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> ResolveException(string id, [FromBody] ResolveExceptionDto dto)
        => Act(await service.ResolveExceptionAsync(id, dto ?? new ResolveExceptionDto(), UserId));

    [HttpPost("{id}/voucher")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> RaiseVoucher(string id, [FromBody] RaiseVoucherDto? dto)
        => Act(await service.RaiseVoucherAsync(id, dto ?? new RaiseVoucherDto(), UserId));

    private IActionResult Act(MatchActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
