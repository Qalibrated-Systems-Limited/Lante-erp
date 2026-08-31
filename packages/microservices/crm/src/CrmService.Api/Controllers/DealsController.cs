using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Deals;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/deals")]
[Authorize]
public class DealsController(IDealService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] DealFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var d = await service.GetByIdAsync(id);
        return d is null ? NotFound(new { message = "Deal not found." }) : Ok(new { data = d });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Create([FromBody] CreateDealDto dto)
        => Ok(new { data = await service.CreateFromOpportunityAsync(dto, UserId) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDealDto dto)
        => Ok(new { data = await service.UpdateAsync(id, dto, UserId) });

    [HttpPost("{id}/contract")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> RegisterContract(string id, [FromBody] RegisterContractDto dto)
        => Ok(new { data = await service.RegisterContractAsync(id, dto, UserId) });

    [HttpPost("{id}/create-project")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateProject(string id)
        => Ok(new { data = await service.CreateProjectAsync(id, UserId) });

    [HttpPost("{id}/close")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Close(string id)
        => Ok(new { data = await service.CloseAsync(id, UserId) });
}
