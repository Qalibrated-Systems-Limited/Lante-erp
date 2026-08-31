using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Board;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// PR4c — the reads behind the Kanban board, the calendar and the workload view.
///
/// The workload route sits at <c>projects/workload</c>; the per-project route carries a
/// <c>:guid</c> constraint, so "workload" cannot be read as a project id.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
[Authorize]
public class ProjectBoardController(IProjectBoardService board) : ControllerBase
{
    private static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    [HttpGet("{projectId:guid}/board")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectBoardDto>>> Board(Guid projectId)
        => Ok(await board.GetBoardAsync(projectId.ToString()));

    /// <summary>Cross-project, so it needs the wider read scope.</summary>
    [HttpGet("workload")]
    [Authorize(Policy = "Permission:projects.read.all")]
    public async Task<ActionResult<ApiResponse<WorkloadDto>>> Workload(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? departmentId)
        => Ok(await board.GetWorkloadAsync(from, to, departmentId));
}
