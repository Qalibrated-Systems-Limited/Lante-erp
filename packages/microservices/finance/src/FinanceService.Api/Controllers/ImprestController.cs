using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

// ── Staff imprest (Process 14) ──
public class ImprestController : BaseFinanceController
{
    private readonly IImprestService _svc;
    public ImprestController(IImprestService svc) => _svc = svc;

    [HttpGet("imprest")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<ImprestReadDto>>.Ok(await _svc.ListAsync()));

    [HttpGet("imprest/advances")]
    public async Task<IActionResult> Advances() => Ok(ApiResponse<List<PersonalAdvanceReadDto>>.Ok(await _svc.ListAdvancesAsync()));

    [HttpPost("imprest")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateImprestDto dto)
        => Ok(ApiResponse<ImprestReadDto>.Ok(await _svc.CreateAsync(dto, CurrentUserId), "Imprest requested."));

    [HttpPost("imprest/{id}/approve")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Approve(string id)
        => Ok(ApiResponse<ImprestReadDto>.Ok(await _svc.ApproveAsync(id, CurrentUserId, ApprovalCtx), "Imprest approved."));

    [HttpPost("imprest/{id}/disburse")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Disburse(string id)
        => Ok(ApiResponse<ImprestReadDto>.Ok(await _svc.DisburseAsync(id, CurrentUserId), "Imprest disbursed & posted."));

    [HttpPost("imprest/{id}/retire")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Retire(string id, [FromBody] RetireImprestDto dto)
        => Ok(ApiResponse<ImprestReadDto>.Ok(await _svc.RetireAsync(id, dto, CurrentUserId), "Imprest retired & posted."));

    /// FIN-012B/C — sweep overdue imprests into payroll-deducted personal advances.
    [HttpPost("imprest/run-conversions")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> RunConversions([FromQuery] DateTime? asOf)
    {
        var created = await _svc.RunConversionsAsync(asOf, CurrentUserId);
        return Ok(ApiResponse<List<PersonalAdvanceReadDto>>.Ok(created, $"{created.Count} imprest(s) converted to advances."));
    }
}
