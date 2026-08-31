using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.PurchaseOrders;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P4 (LPO step 5) — Local Purchase Orders. Generated from an approved PR with a completed
/// comparison, routed through the value-based approval authority matrix (each step digitally signed);
/// &gt; 500k requires a Board Resolution before the MD signs. Final approval posts the purchase-commitment
/// journal to Finance and issues the LPO.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/purchase-orders")]
[Authorize]
public class PurchaseOrdersController(IPurchaseOrderService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] PoFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize, pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var po = await service.GetByIdAsync(id);
        return po is null ? NotFound(new { message = "LPO not found." }) : Ok(new { data = po });
    }

    [HttpGet("by-requisition/{prId}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetByPr(string prId)
    {
        var po = await service.GetByPrAsync(prId);
        return po is null ? NotFound(new { message = "No LPO for this requisition." }) : Ok(new { data = po });
    }

    [HttpPost("generate/{prId}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Generate(string prId, [FromBody] GenerateLpoDto dto)
        => Act(await service.GenerateAsync(prId, dto ?? new GenerateLpoDto(), UserId));

    [HttpPost("{id}/board-resolution")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> BoardResolution(string id, [FromBody] BoardResolutionDto dto)
        => Act(await service.AttachBoardResolutionAsync(id, dto, UserId));

    [HttpPost("{id}/sign")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Sign(string id, [FromBody] SignLpoDto dto)
        => Act(await service.SignAsync(id, dto, UserId, UserName));

    private IActionResult Act(PoActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
