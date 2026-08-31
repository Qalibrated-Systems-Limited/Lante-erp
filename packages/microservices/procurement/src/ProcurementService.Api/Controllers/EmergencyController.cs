using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Emergency;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P8 (PROC-004) — emergency procurement. Sourcing is waived and nothing else: the MD must authorise
/// before the purchase is made (never retrospectively), justification and the quotation waiver are mandatory,
/// a post-hoc requisition is due within 24 hours, and every case is reported in the monthly board pack.
/// Receipt, the 3-way match and payment authority all still apply.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/emergency")]
[Authorize]
public class EmergencyController(IEmergencyProcurementService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] EmergencyFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize, pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    /// <summary>Everything declared in a period ("YYYY-MM"), for the monthly board pack.</summary>
    [HttpGet("board-pack")]
    [Authorize(Policy = "Permission:procurement.read.all")]
    public async Task<IActionResult> BoardPack([FromQuery] string period)
        => Ok(new { data = await service.GetBoardPackAsync(period), period });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var r = await service.GetByIdAsync(id);
        return r is null ? NotFound(new { message = "Emergency record not found." }) : Ok(new { data = r });
    }

    [HttpGet("by-po/{poId}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetByPo(string poId)
    {
        var r = await service.GetByPoAsync(poId);
        return r is null ? NotFound(new { message = "This LPO is not an emergency purchase." }) : Ok(new { data = r });
    }

    [HttpPost("declare")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Declare([FromBody] DeclareEmergencyDto dto)
        => Act(await service.DeclareAsync(dto, UserId));

    /// <summary>The PROC-004 key control — MD authorisation, which is what issues the emergency LPO.</summary>
    [HttpPost("{id}/md-approve")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> MdApprove(string id, [FromBody] MdApproveEmergencyDto dto)
        => Act(await service.MdApproveAsync(id, dto, UserId, UserName));

    [HttpPost("{id}/post-hoc-pr")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> PostHocPr(string id, [FromBody] PostHocPrDto? dto)
        => Act(await service.RaisePostHocPrAsync(id, dto ?? new PostHocPrDto(), UserId));

    [HttpPost("{id}/board-pack")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> MarkBoardPack(string id, [FromBody] BoardPackDto dto)
        => Act(await service.MarkBoardPackAsync(id, dto, UserId));

    private IActionResult Act(EmergencyActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
