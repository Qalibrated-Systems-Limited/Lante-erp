using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Leads;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/leads")]
[Authorize]
public class LeadsController(ILeadService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] LeadFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new
        {
            data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize),
            openCount = r.OpenCount, openValue = r.OpenValue,
        });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var l = await service.GetByIdAsync(id);
        return l is null ? NotFound(new { message = "Lead not found." }) : Ok(new { data = l });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Create([FromBody] CreateLeadDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId, UserName) });

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateLeadDto dto)
        => Ok(new { data = await service.UpdateAsync(id, dto, UserId) });

    [HttpPost("{id}/activities")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> AddActivity(string id, [FromBody] CreateLeadActivityDto dto)
        => Ok(new { data = await service.AddActivityAsync(id, dto, UserId) });

    [HttpPost("{id}/assign")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Assign(string id, [FromBody] AssignLeadDto dto)
        => Ok(new { data = await service.AssignAsync(id, dto, UserId) });

    [HttpPost("{id}/qualify")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Qualify(string id, [FromBody] QualifyLeadDto dto)
        => Ok(new { data = await service.QualifyAsync(id, dto, UserId) });

    [HttpPost("{id}/unqualify")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Unqualify(string id, [FromBody] UnqualifyLeadDto dto)
        => Ok(new { data = await service.UnqualifyAsync(id, dto, UserId) });

    [HttpPost("{id}/convert")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Convert(string id, [FromBody] ConvertLeadBody? body)
        => Ok(new { data = await service.ConvertAsync(id, body?.CustomerId, UserId) });

    public record ConvertLeadBody(string? CustomerId);
}
