using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// STAT-009: Company Secretary workflow — AGM scheduling, annual-return preparation and
// statutory-register updates — task list with deadlines and responsible person.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cosec-tasks")]
public class CosecTasksController(
    IComplianceCrudService<CosecTask> tasks,
    ICosecTaskRepository tasksRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<CosecTaskReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await tasksRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<CosecTaskReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<CosecTaskReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<CosecTaskReadDto>>> GetById(string id)
    {
        var t = await tasks.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<CosecTaskReadDto>.Fail("Task not found.", 404));
        return Ok(ApiResponse<CosecTaskReadDto>.Ok(ToReadDto(t)));
    }

    [HttpPost]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<CosecTaskReadDto>>> Create([FromBody] CreateCosecTaskDto dto)
    {
        var t = new CosecTask
        {
            ObligationId = dto.ObligationId,
            Title = dto.Title,
            ResponsiblePersonUserId = dto.ResponsiblePersonUserId,
            ResponsiblePersonName = dto.ResponsiblePersonName,
            DueDate = dto.DueDate,
        };
        await tasks.CreateAsync(t);
        return CreatedAtAction(nameof(GetById), new { id = t.Id, version = "1" },
            ApiResponse<CosecTaskReadDto>.Ok(ToReadDto(t), "Task added."));
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<CosecTaskReadDto>>> UpdateStatus(string id, [FromBody] UpdateCosecTaskStatusDto dto)
    {
        var t = await tasks.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<CosecTaskReadDto>.Fail("Task not found.", 404));

        t.Status = dto.Status;
        await tasks.UpdateAsync(t);
        return Ok(ApiResponse<CosecTaskReadDto>.Ok(ToReadDto(t), "Task updated."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<CosecTaskReadDto>>> Update(string id, [FromBody] UpdateCosecTaskDto dto)
    {
        var t = await tasks.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<CosecTaskReadDto>.Fail("Task not found.", 404));

        t.ObligationId = dto.ObligationId;
        t.Title = dto.Title;
        t.ResponsiblePersonUserId = dto.ResponsiblePersonUserId;
        t.ResponsiblePersonName = dto.ResponsiblePersonName;
        t.DueDate = dto.DueDate;
        await tasks.UpdateAsync(t);

        return Ok(ApiResponse<CosecTaskReadDto>.Ok(ToReadDto(t), "Task updated."));
    }

    private static CosecTaskReadDto ToReadDto(CosecTask t) => new()
    {
        Id = t.Id,
        ObligationId = t.ObligationId,
        Title = t.Title,
        ResponsiblePersonUserId = t.ResponsiblePersonUserId,
        ResponsiblePersonName = t.ResponsiblePersonName,
        DueDate = t.DueDate,
        Status = t.Status,
    };
}
