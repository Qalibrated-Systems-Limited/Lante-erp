using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Macros;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/macros")]
public class MacrosController(IMacroService macroService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MacroReadDto>>>> GetAll([FromQuery] string? categoryId = null)
    {
        var macros = await macroService.GetAllAsync(categoryId);
        return Ok(ApiResponse<IEnumerable<MacroReadDto>>.Ok(macros));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<MacroReadDto>>> GetById(string id)
    {
        var macro = await macroService.GetByIdAsync(id);
        if (macro == null) return NotFound(ApiResponse<MacroReadDto>.Fail("Macro not found.", 404));
        return Ok(ApiResponse<MacroReadDto>.Ok(macro));
    }

    [HttpPost]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<MacroReadDto>>> Create([FromBody] CreateMacroDto dto)
    {
        var created = await macroService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" },
            ApiResponse<MacroReadDto>.Ok(created, "Macro created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<MacroReadDto>>> Update(string id, [FromBody] UpdateMacroDto dto)
    {
        var updated = await macroService.UpdateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<MacroReadDto>.Ok(updated, "Macro updated successfully."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        await macroService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "Macro deleted successfully."));
    }

    [HttpPost("apply/{ticketId}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<CommentReadDto>>> ApplyToTicket(string ticketId, [FromBody] ApplyMacroDto dto)
    {
        var comment = await macroService.ApplyToTicketAsync(ticketId, dto, CurrentUserId);
        return Ok(ApiResponse<CommentReadDto>.Ok(comment, "Macro applied to ticket."));
    }
}
