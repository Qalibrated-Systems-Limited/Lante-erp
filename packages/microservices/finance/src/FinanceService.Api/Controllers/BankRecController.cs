using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

// ── Bank reconciliation (Processes 9/10) ──
public class BankRecController : BaseFinanceController
{
    private readonly IBankRecService _svc;
    public BankRecController(IBankRecService svc) => _svc = svc;

    [HttpGet("bank-rec")]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<ReconciliationSummaryDto>>.Ok(await _svc.ListAsync()));

    [HttpGet("bank-rec/{id}")]
    public async Task<IActionResult> Get(string id) => Ok(ApiResponse<ReconciliationReadDto>.Ok(await _svc.GetAsync(id)));

    [HttpPost("bank-rec")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateReconciliationDto dto)
        => Ok(ApiResponse<ReconciliationReadDto>.Ok(await _svc.CreateAsync(dto, CurrentUserId), "Reconciliation created & auto-matched."));

    [HttpPost("bank-rec/{id}/lines/{lineId}/match")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Match(string id, string lineId, [FromQuery] string glEntryId)
        => Ok(ApiResponse<ReconciliationReadDto>.Ok(await _svc.MatchAsync(id, lineId, glEntryId, CurrentUserId), "Matched."));

    [HttpPost("bank-rec/{id}/lines/{lineId}/unmatch")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Unmatch(string id, string lineId)
        => Ok(ApiResponse<ReconciliationReadDto>.Ok(await _svc.UnmatchAsync(id, lineId, CurrentUserId), "Unmatched."));

    [HttpPost("bank-rec/{id}/lines/{lineId}/post")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Post(string id, string lineId, [FromBody] PostBankItemDto dto)
        => Ok(ApiResponse<ReconciliationReadDto>.Ok(await _svc.PostBankItemAsync(id, lineId, dto, CurrentUserId), "Posted to the ledger & matched."));

    [HttpPost("bank-rec/{id}/complete")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Complete(string id)
        => Ok(ApiResponse<ReconciliationReadDto>.Ok(await _svc.CompleteAsync(id, CurrentUserId), "Reconciliation completed."));
}
