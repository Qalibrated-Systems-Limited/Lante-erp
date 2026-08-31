using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Quotations;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/quotations")]
[Authorize]
public class QuotationsController(IQuotationService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] QuotationFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var q = await service.GetByIdAsync(id);
        return q is null ? NotFound(new { message = "Quotation not found." }) : Ok(new { data = q });
    }

    [HttpGet("number/{quoteNumber}/versions")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Versions(string quoteNumber) => Ok(new { data = await service.GetVersionsAsync(quoteNumber) });

    [HttpGet("last-sale")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> LastSale([FromQuery] string? customerId, [FromQuery] string productRef)
        => Ok(new { data = await service.GetLastSaleAsync(customerId, productRef) });

    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Create([FromBody] CreateQuotationDto dto)
        => Ok(new { data = await service.CreateAsync(dto, UserId) });

    [HttpPut("{id}/lines")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SaveLines(string id, [FromBody] SaveQuotationLinesDto dto)
        => Ok(new { data = await service.SaveLinesAsync(id, dto, UserId) });

    [HttpPost("{id}/submit")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Submit(string id) => Ok(new { data = await service.SubmitAsync(id, UserId) });

    [HttpPost("{id}/dept-head-review")]
    [Authorize(Policy = "Permission:crm.approve.bd")]
    public async Task<IActionResult> DeptHeadReview(string id, [FromBody] ReviewBody body)
        => Ok(new { data = await service.DeptHeadReviewAsync(id, body.Approve, body.Reason, UserId) });

    [HttpPost("{id}/md-approve")]
    [Authorize(Policy = "Permission:crm.approve.md")]
    public async Task<IActionResult> MdApprove(string id) => Ok(new { data = await service.MdApproveAsync(id, UserId) });

    [HttpPost("{id}/send")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Send(string id) => Ok(new { data = await service.SendAsync(id, UserId) });

    [HttpPost("{id}/outcome")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Outcome(string id, [FromBody] QuotationOutcomeDto dto)
        => Ok(new { data = await service.RecordOutcomeAsync(id, dto, UserId) });

    [HttpPost("{id}/revise")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Revise(string id) => Ok(new { data = await service.ReviseAsync(id, UserId) });

    // ── Price list ──
    [HttpGet("price-list")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> PriceList() => Ok(new { data = await service.GetPriceListsAsync() });

    [HttpPost("price-list")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SavePriceList([FromBody] SavePriceListDto dto)
        => Ok(new { data = await service.SavePriceListAsync(dto, UserId) });

    public record ReviewBody(bool Approve, string? Reason);
}
