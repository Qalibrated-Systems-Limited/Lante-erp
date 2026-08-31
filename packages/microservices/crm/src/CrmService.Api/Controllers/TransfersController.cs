using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Transfers;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/transfers")]
[Authorize]
public class TransfersController(ITransferService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] TransferFilterParams filter)
    {
        var r = await service.GetAllAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, page = filter.Page, pageSize = filter.PageSize,
            pages = (int)Math.Ceiling(r.Total / (double)filter.PageSize) });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var t = await service.GetByIdAsync(id);
        return t is null ? NotFound(new { message = "Transfer not found." }) : Ok(new { data = t });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Raise([FromBody] RaiseTransferDto dto) => Ok(new { data = await service.RaiseAsync(dto, UserId) });

    [HttpPost("{id}/approve/head-bd")]
    [Authorize(Policy = "Permission:crm.approve.bd")]
    public async Task<IActionResult> ApproveHeadBd(string id) => Ok(new { data = await service.ApproveHeadBdAsync(id, UserId) });

    [HttpPost("{id}/approve/cfo")]
    [Authorize(Policy = "Permission:crm.approve.cfo")]
    public async Task<IActionResult> ApproveCfo(string id) => Ok(new { data = await service.ApproveCfoAsync(id, UserId) });

    [HttpPost("{id}/approve/md")]
    [Authorize(Policy = "Permission:crm.approve.md")]
    public async Task<IActionResult> ApproveMd(string id) => Ok(new { data = await service.ApproveMdAsync(id, UserId) });

    [HttpPost("{id}/reject")]
    [Authorize(Policy = "Permission:crm.approve.bd")]
    public async Task<IActionResult> Reject(string id, [FromBody] RejectTransferDto dto) => Ok(new { data = await service.RejectAsync(id, dto, UserId) });

    [HttpPut("{id}/handover")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> UpdateHandover(string id, [FromBody] UpdateHandoverDto dto) => Ok(new { data = await service.UpdateHandoverAsync(id, dto, UserId) });

    [HttpPost("{id}/handover/sign")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Sign(string id, [FromBody] SignHandoverDto dto) => Ok(new { data = await service.SignHandoverAsync(id, dto, UserId) });

    [HttpPost("{id}/complete")]
    [Authorize(Policy = "Permission:crm.approve.md")]
    public async Task<IActionResult> Complete(string id) => Ok(new { data = await service.CompleteAsync(id, UserId) });
}
