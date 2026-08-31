using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Opportunities;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/opportunities")]
[Authorize]
public class OpportunitiesController(IOpportunityService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet("stages")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Stages() => Ok(new { data = await service.GetStagesAsync() });

    [HttpGet("board")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Board([FromQuery] string? assignedTo)
    {
        var b = await service.GetBoardAsync(assignedTo);
        return Ok(new { data = b.Columns, totalValue = b.TotalValue, totalWeighted = b.TotalWeighted, openCount = b.OpenCount });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] OpportunityFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var o = await service.GetByIdAsync(id);
        return o is null ? NotFound(new { message = "Opportunity not found." }) : Ok(new { data = o });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId, UserName) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateOpportunityDto dto)
        => Ok(new { data = await service.UpdateAsync(id, dto, UserId) });

    [HttpPost("{id}/advance")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Advance(string id, [FromBody] AdvanceStageDto dto)
        => Ok(new { data = await service.AdvanceStageAsync(id, dto, UserId) });

    [HttpPost("{id}/won")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Won(string id)
        => Ok(new { data = await service.MarkWonAsync(id, UserId) });

    [HttpPost("{id}/lost")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Lost(string id, [FromBody] MarkLostDto dto)
        => Ok(new { data = await service.MarkLostAsync(id, dto, UserId) });

    [HttpPost("{id}/activities")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> AddActivity(string id, [FromBody] CreateOpportunityActivityDto dto)
        => Ok(new { data = await service.AddActivityAsync(id, dto, UserId) });
}
