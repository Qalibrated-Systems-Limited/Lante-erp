using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Requisitions;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P2 (LPO steps 1–2) — Purchase Requisitions. Build → submit (hard budget check) → Dept-Head
/// review within a 2-business-day SLA.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/requisitions")]
[Authorize]
public class RequisitionsController(IPurchaseRequisitionService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] PrFilterParams filter)
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
        var pr = await service.GetByIdAsync(id);
        return pr is null ? NotFound(new { message = "Requisition not found." }) : Ok(new { data = pr });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Create([FromBody] CreatePrDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId, UserName) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Update(string id, [FromBody] CreatePrDto dto)
    {
        var pr = await service.UpdateAsync(id, dto, UserId);
        return pr is null ? NotFound(new { message = "Requisition not found." }) : Ok(new { data = pr });
    }

    [HttpPost("{id}/submit")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Submit(string id) => Act(await service.SubmitAsync(id, UserId, UserName));

    [HttpPost("{id}/review")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Review(string id, [FromBody] ReviewPrDto dto)
        => Act(await service.ReviewAsync(id, dto, UserId, UserName));

    [HttpPost("{id}/escalate")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Escalate(string id) => Act(await service.EscalateAsync(id, UserId));

    private IActionResult Act(PrActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message })
         : r.Status == "Blocked" ? BadRequest(new { message = r.Message, blocked = true })
         : Ok(new { data = r });
}
