using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Workflow;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workflow-rules")]
public class WorkflowRulesController(IWorkflowRuleService workflowRuleService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<WorkflowRuleReadDto>>>> GetAll()
    {
        var rules = await workflowRuleService.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<WorkflowRuleReadDto>>.Ok(rules));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<WorkflowRuleReadDto>>> GetById(string id)
    {
        var rule = await workflowRuleService.GetByIdAsync(id);
        if (rule == null) return NotFound(ApiResponse<WorkflowRuleReadDto>.Fail("Workflow rule not found.", 404));
        return Ok(ApiResponse<WorkflowRuleReadDto>.Ok(rule));
    }

    [HttpPost]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<WorkflowRuleReadDto>>> Create([FromBody] CreateWorkflowRuleDto dto)
    {
        var created = await workflowRuleService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" },
            ApiResponse<WorkflowRuleReadDto>.Ok(created, "Workflow rule created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<WorkflowRuleReadDto>>> Update(string id, [FromBody] UpdateWorkflowRuleDto dto)
    {
        var updated = await workflowRuleService.UpdateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<WorkflowRuleReadDto>.Ok(updated, "Workflow rule updated successfully."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        await workflowRuleService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "Workflow rule deleted successfully."));
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Activate(string id)
    {
        await workflowRuleService.SetActiveAsync(id, true, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(null!, "Workflow rule activated."));
    }

    [HttpPatch("{id}/deactivate")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(string id)
    {
        await workflowRuleService.SetActiveAsync(id, false, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(null!, "Workflow rule deactivated."));
    }
}
