using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Incidents;
using HSEService.Core.Entities;
using HSEService.Core.Enums;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-corrective-actions")]
public class CorrectiveActionsController(IHseCrudService<CorrectiveAction> correctiveActions, ICorrectiveActionRepository correctiveActionRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<CorrectiveActionReadDto>>>> GetAll([FromQuery] CorrectiveActionFilterParameters parameters)
    {
        var paged = await correctiveActionRepository.GetPagedAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<CorrectiveActionReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<CorrectiveActionReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<CorrectiveActionReadDto>>> GetById(string id)
    {
        var capa = await correctiveActions.GetByIdAsync(id);
        if (capa == null) return NotFound(ApiResponse<CorrectiveActionReadDto>.Fail("Corrective action not found.", 404));
        return Ok(ApiResponse<CorrectiveActionReadDto>.Ok(ToReadDto(capa)));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<CorrectiveActionReadDto>>> Update(string id, [FromBody] UpdateCorrectiveActionDto dto)
    {
        var capa = await correctiveActions.GetByIdAsync(id);
        if (capa == null) return NotFound(ApiResponse<CorrectiveActionReadDto>.Fail("Corrective action not found.", 404));

        capa.Description = dto.Description;
        capa.OwnerUserId = dto.OwnerUserId;
        capa.OwnerName = dto.OwnerName;
        capa.Status = dto.Status;
        capa.DueDate = dto.DueDate;
        capa.ClosedAt = dto.Status == CorrectiveActionStatus.Completed ? (capa.ClosedAt ?? DateTime.UtcNow) : null;
        await correctiveActions.UpdateAsync(capa);
        return Ok(ApiResponse<CorrectiveActionReadDto>.Ok(ToReadDto(capa), "Corrective action updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await correctiveActions.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Corrective action not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Corrective action deleted successfully.", StatusCode = 200 });
    }

    private static CorrectiveActionReadDto ToReadDto(CorrectiveAction c) => new()
    {
        Id = c.Id,
        IncidentId = c.IncidentId,
        Description = c.Description,
        OwnerUserId = c.OwnerUserId,
        OwnerName = c.OwnerName,
        Status = c.Status,
        DueDate = c.DueDate,
        ClosedAt = c.ClosedAt,
    };
}
