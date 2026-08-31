using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Categories;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ticket-categories")]
public class TicketCategoriesController(
    ITicketCategoryService categoryService,
    ISLAService slaService,
    IEscalationService escalationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CategoryReadDto>>>> GetAll()
    {
        var categories = await categoryService.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<CategoryReadDto>>.Ok(categories));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<CategoryReadDto>>> GetById(string id)
    {
        var category = await categoryService.GetByIdAsync(id);
        if (category == null) return NotFound(ApiResponse<CategoryReadDto>.Fail("Category not found.", 404));
        return Ok(ApiResponse<CategoryReadDto>.Ok(category));
    }

    [HttpPost]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<CategoryReadDto>>> Create([FromBody] CreateCategoryDto dto)
    {
        var created = await categoryService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" },
            ApiResponse<CategoryReadDto>.Ok(created, "Category created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<CategoryReadDto>>> Update(string id, [FromBody] UpdateCategoryDto dto)
    {
        var updated = await categoryService.UpdateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<CategoryReadDto>.Ok(updated, "Category updated successfully."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        await categoryService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "Category deleted successfully."));
    }

    [HttpGet("{id}/sla-policies")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<SLAPolicyReadDto>>>> GetSLAPolicies(string id)
    {
        var policies = await slaService.GetPoliciesByCategoryAsync(id);
        return Ok(ApiResponse<IEnumerable<SLAPolicyReadDto>>.Ok(policies));
    }

    [HttpPost("{id}/sla-policies")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<SLAPolicyReadDto>>> AddSLAPolicy(string id, [FromBody] CreateSLAPolicyDto dto)
    {
        dto.CategoryId = id;
        var result = await slaService.AddPolicyAsync(dto, CurrentUserId);
        return Ok(ApiResponse<SLAPolicyReadDto>.Ok(result, "SLA policy added."));
    }

    [HttpGet("{id}/escalation-rules")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<EscalationRuleReadDto>>>> GetEscalationRules(string id)
    {
        var rules = await escalationService.GetRulesByCategoryAsync(id);
        return Ok(ApiResponse<IEnumerable<EscalationRuleReadDto>>.Ok(rules));
    }

    [HttpPost("{id}/escalation-rules")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<EscalationRuleReadDto>>> AddEscalationRule(string id, [FromBody] CreateEscalationRuleDto dto)
    {
        dto.CategoryId = id;
        var result = await escalationService.AddRuleAsync(dto, CurrentUserId);
        return Ok(ApiResponse<EscalationRuleReadDto>.Ok(result, "Escalation rule added."));
    }
}
