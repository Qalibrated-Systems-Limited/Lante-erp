using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.ToolboxTalks;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-004: toolbox talk register — date, site, topic, attendees, signed off by Site Supervisor.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-toolbox-talks")]
public class ToolboxTalksController(IToolboxTalkWorkflowService workflow, IHseCrudService<ToolboxTalk> toolboxTalks) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ToolboxTalkReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await workflow.GetPagedWithAttendeesAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<ToolboxTalkReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<ToolboxTalkReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<ToolboxTalkReadDto>>> GetById(string id)
    {
        var talk = await workflow.GetWithAttendeesAsync(id);
        if (talk == null) return NotFound(ApiResponse<ToolboxTalkReadDto>.Fail("Toolbox talk not found.", 404));
        return Ok(ApiResponse<ToolboxTalkReadDto>.Ok(ToReadDto(talk)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<ToolboxTalkReadDto>>> Create([FromBody] CreateToolboxTalkDto dto)
    {
        var talk = await workflow.CreateWithAttendeesAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = talk.Id, version = "1" },
            ApiResponse<ToolboxTalkReadDto>.Ok(ToReadDto(talk), "Toolbox talk recorded."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<ToolboxTalkReadDto>>> Update(string id, [FromBody] UpdateToolboxTalkDto dto)
    {
        var talk = await toolboxTalks.GetByIdAsync(id);
        if (talk == null) return NotFound(ApiResponse<ToolboxTalkReadDto>.Fail("Toolbox talk not found.", 404));

        talk.SiteId = dto.SiteId;
        talk.SiteName = dto.SiteName;
        talk.SupervisorUserId = dto.SupervisorUserId;
        talk.SupervisorName = dto.SupervisorName;
        talk.Topic = dto.Topic;
        talk.HeldOn = dto.HeldOn;
        await toolboxTalks.UpdateAsync(talk);

        var withAttendees = await workflow.GetWithAttendeesAsync(id);
        return Ok(ApiResponse<ToolboxTalkReadDto>.Ok(ToReadDto(withAttendees!), "Toolbox talk updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await toolboxTalks.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Toolbox talk not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Toolbox talk deleted successfully.", StatusCode = 200 });
    }

    private static ToolboxTalkReadDto ToReadDto(ToolboxTalk t) => new()
    {
        Id = t.Id,
        SiteId = t.SiteId,
        SiteName = t.SiteName,
        SupervisorUserId = t.SupervisorUserId,
        SupervisorName = t.SupervisorName,
        Topic = t.Topic,
        HeldOn = t.HeldOn,
        Attendees = t.Attendees.Select(a => new ToolboxAttendeeDto
        {
            EmployeeUserId = a.EmployeeUserId,
            EmployeeName = a.EmployeeName,
        }).ToList(),
    };
}
