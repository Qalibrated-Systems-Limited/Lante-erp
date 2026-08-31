using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Performance;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P9 (PROC-002) — biannual supplier performance review. Scores approved, non-blacklisted suppliers
/// from transaction data already in the system (Quality 30 / Delivery 25 / Pricing 25 / Compliance 20) and
/// feeds the result back into the ASR. Below 60 warns; below 40 escalates to the MD, who decides on
/// blacklisting through the supplier register — the review never blacklists on its own.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/performance")]
[Authorize]
public class PerformanceController(IPerformanceReviewService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] PerformanceFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize, pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Summary([FromQuery] string? period)
        => Ok(new { data = await service.GetSummaryAsync(period) });

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var r = await service.GetByIdAsync(id);
        return r is null ? NotFound(new { message = "Review not found." }) : Ok(new { data = r });
    }

    /// <summary>A supplier's score history across periods (its ASR trend).</summary>
    [HttpGet("supplier/{supplierId}")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> History(string supplierId)
        => Ok(new { data = await service.GetHistoryAsync(supplierId) });

    /// <summary>Runs the biannual review across every eligible supplier. Idempotent per supplier and period.</summary>
    [HttpPost("run")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Run([FromBody] RunReviewDto? dto)
        => Act(await service.RunAsync(dto ?? new RunReviewDto(), UserId));

    [HttpPost("run/{supplierId}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> RunForSupplier(string supplierId, [FromBody] RunReviewDto? dto)
        => Act(await service.RunForSupplierAsync(supplierId, dto ?? new RunReviewDto(), UserId));

    [HttpPost("{id}/escalate")]
    [Authorize(Policy = "Permission:procurement.approve")]
    public async Task<IActionResult> Escalate(string id, [FromBody] EscalateReviewDto? dto)
        => Act(await service.EscalateAsync(id, dto ?? new EscalateReviewDto(), UserId));

    private IActionResult Act(PerformanceActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
