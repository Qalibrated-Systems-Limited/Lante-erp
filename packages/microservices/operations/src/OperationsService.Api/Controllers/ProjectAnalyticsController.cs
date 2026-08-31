using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Analytics;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// PR4a — earned value and portfolio reporting.
///
/// The portfolio route sits at <c>projects/portfolio/evm</c>; the per-project routes carry a
/// <c>:guid</c> constraint, so "portfolio" cannot be mistaken for a project id.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
[Authorize]
public class ProjectAnalyticsController(IProjectAnalyticsService analytics) : ControllerBase
{
    private static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    [HttpGet("{projectId:guid}/evm")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectEvmDto>>> ProjectEvm(Guid projectId, [FromQuery] DateTime? asOf)
        => Ok(await analytics.GetProjectEvmAsync(projectId.ToString(), asOf));

    /// <summary>Cross-project rollup — needs the wider read scope, not just one's own projects.</summary>
    [HttpGet("portfolio/evm")]
    [Authorize(Policy = "Permission:projects.read.all")]
    public async Task<ActionResult<ApiResponse<PortfolioEvmDto>>> PortfolioEvm([FromQuery] DateTime? asOf)
        => Ok(await analytics.GetPortfolioEvmAsync(asOf));
}
