using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.International;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P7 (PROC-003) — China / international sourcing. Foreign-currency detail attached to an issued
/// LPO: FX terms, the T/T advance (MD approval mandatory before funds move), shipment and customs tracking,
/// and the landed-cost build-up that yields a true cost per unit.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/international")]
[Authorize]
public class InternationalController(IInternationalPoService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] IntlFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize, pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    /// <summary>Currencies + rates Finance holds, so the UI shows the rate that will be applied.</summary>
    [HttpGet("currencies")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Currencies() => Ok(new { data = await service.GetCurrenciesAsync() });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var r = await service.GetByIdAsync(id);
        return r is null ? NotFound(new { message = "International order not found." }) : Ok(new { data = r });
    }

    [HttpGet("by-po/{poId}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetByPo(string poId)
    {
        var r = await service.GetByPoAsync(poId);
        return r is null ? NotFound(new { message = "This LPO has no international detail." }) : Ok(new { data = r });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Create([FromBody] CreateIntlPoDto dto)
        => Act(await service.CreateAsync(dto, UserId));

    [HttpPut("{id}/shipment")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Shipment(string id, [FromBody] ShipmentDto dto)
        => Act(await service.UpdateShipmentAsync(id, dto, UserId));

    // ── T/T advance ──
    [HttpPost("{id}/tt/request")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> RequestTt(string id, [FromBody] RequestTtDto dto)
        => Act(await service.RequestTtAsync(id, dto, UserId));

    /// <summary>The PROC-003 key control — MD approval before any funds are transferred.</summary>
    [HttpPost("{id}/tt/approve")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> ApproveTt(string id) => Act(await service.ApproveTtAsync(id, UserId));

    [HttpPost("{id}/tt/send")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> SendTt(string id) => Act(await service.SendTtAsync(id, UserId));

    // ── Landed cost ──
    [HttpPost("{id}/components")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> AddComponent(string id, [FromBody] AddComponentDto dto)
        => Act(await service.AddComponentAsync(id, dto, UserId));

    [HttpDelete("components/{componentId}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> RemoveComponent(string componentId)
        => Act(await service.RemoveComponentAsync(componentId, UserId));

    [HttpPost("{id}/customs")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> DeclareCustoms(string id, [FromBody] DeclareCustomsDto dto)
        => Act(await service.DeclareCustomsAsync(id, dto, UserId));

    [HttpPost("{id}/finalise-cost")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> FinaliseCost(string id) => Act(await service.FinaliseCostAsync(id, UserId));

    private IActionResult Act(IntlActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
