using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Quotations;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P3 — quotation &amp; comparative analysis. Quotes are recorded against an approved requisition
/// (ASR-approved suppliers only); the comparison enforces the threshold band's minimum quote count and,
/// once completed, unblocks P4 LPO generation.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/requisitions/{prId}")]
[Authorize]
public class QuotationsController(IQuotationService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("sourcing")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Sourcing(string prId) => Ok(new { data = await service.GetSourcingAsync(prId) });

    [HttpGet("comparison")]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> Comparison(string prId)
    {
        var c = await service.GetComparisonAsync(prId);
        return c is null ? NotFound(new { message = "Requisition not found." }) : Ok(new { data = c });
    }

    [HttpPost("quotations")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Record(string prId, [FromBody] RecordQuotationDto dto)
        => Act(await service.RecordQuotationAsync(prId, dto, UserId));

    [HttpPost("complete-comparison")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Complete(string prId, [FromBody] CompleteComparisonDto dto)
        => Act(await service.CompleteComparisonAsync(prId, dto, UserId));

    private IActionResult Act(QuotationActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message })
         : r.Status == "Blocked" ? BadRequest(new { message = r.Message, blocked = true })
         : Ok(new { data = r });
}

/// <summary>P3 — actions on an individual quotation (scoring, removal).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/quotations")]
[Authorize]
public class QuotationItemsController(IQuotationService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpPost("{quotationId}/score")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Score(string quotationId, [FromBody] ScoreQuotationDto dto)
        => Act(await service.ScoreQuotationAsync(quotationId, dto, UserId));

    [HttpDelete("{quotationId}")]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Delete(string quotationId)
        => Act(await service.DeleteQuotationAsync(quotationId, UserId));

    private IActionResult Act(QuotationActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
