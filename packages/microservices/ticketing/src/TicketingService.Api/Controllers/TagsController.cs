using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Tags;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tags")]
public class TagsController(ITagService tagService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TagReadDto>>>> GetAll()
    {
        var tags = await tagService.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<TagReadDto>>.Ok(tags));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<TagReadDto>>> GetById(string id)
    {
        var tag = await tagService.GetByIdAsync(id);
        if (tag == null) return NotFound(ApiResponse<TagReadDto>.Fail("Tag not found.", 404));
        return Ok(ApiResponse<TagReadDto>.Ok(tag));
    }

    [HttpPost]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<TagReadDto>>> Create([FromBody] CreateTagDto dto)
    {
        var created = await tagService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" },
            ApiResponse<TagReadDto>.Ok(created, "Tag created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<TagReadDto>>> Update(string id, [FromBody] UpdateTagDto dto)
    {
        var updated = await tagService.UpdateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TagReadDto>.Ok(updated, "Tag updated successfully."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "settings.manage")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        await tagService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null!, "Tag deleted successfully."));
    }

    // --- Ticket tagging endpoints ---

    [HttpGet("tickets/{ticketId}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TagReadDto>>>> GetTicketTags(string ticketId)
    {
        var tags = await tagService.GetTicketTagsAsync(ticketId);
        return Ok(ApiResponse<IEnumerable<TagReadDto>>.Ok(tags));
    }

    [HttpPost("tickets/{ticketId}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<object>>> AddTagToTicket(string ticketId, [FromBody] AddTagToTicketDto dto)
    {
        await tagService.AddTagToTicketAsync(ticketId, dto.TagId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(null!, "Tag added to ticket."));
    }

    [HttpDelete("tickets/{ticketId}/{tagId}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveTagFromTicket(string ticketId, string tagId)
    {
        await tagService.RemoveTagFromTicketAsync(ticketId, tagId);
        return Ok(ApiResponse<object>.Ok(null!, "Tag removed from ticket."));
    }
}
