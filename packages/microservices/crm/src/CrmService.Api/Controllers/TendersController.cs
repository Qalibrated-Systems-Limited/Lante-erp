using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Tenders;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenders")]
[Authorize]
public class TendersController(ITenderService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] TenderFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize),
            openCount = r.OpenCount, dueSoonCount = r.DueSoonCount, openValue = r.OpenValue });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var t = await service.GetByIdAsync(id);
        return t is null ? NotFound(new { message = "Tender not found." }) : Ok(new { data = t });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Create([FromBody] CreateTenderDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId, UserName) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTenderDto dto)
        => Ok(new { data = await service.UpdateAsync(id, dto, UserId) });

    [HttpPost("{id}/bid-bond")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SaveBidBond(string id, [FromBody] SaveBidBondDto dto)
        => Ok(new { data = await service.SaveBidBondAsync(id, dto, UserId) });

    [HttpPost("{id}/submit")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Submit(string id) => Ok(new { data = await service.SubmitAsync(id, UserId) });

    [HttpPost("{id}/won")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Won(string id, [FromBody] TenderOutcomeDto dto) => Ok(new { data = await service.MarkWonAsync(id, dto, UserId) });

    [HttpPost("{id}/lost")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Lost(string id, [FromBody] TenderOutcomeDto dto) => Ok(new { data = await service.MarkLostAsync(id, dto, UserId) });

    [HttpPost("{id}/no-bid")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> NoBid(string id, [FromBody] TenderOutcomeDto dto) => Ok(new { data = await service.MarkNoBidAsync(id, dto, UserId) });
}
