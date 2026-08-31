using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

public class JournalsController : BaseFinanceController
{
    private readonly IJournalService _journals;
    public JournalsController(IJournalService journals) => _journals = journals;

    /// The backbone posting endpoint. UI raises drafts; other modules pass PostImmediately=true
    /// with sourceModule/sourceDocumentId to post directly.
    [HttpPost("journals")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Create([FromBody] CreateJournalDto dto)
        => Ok(ApiResponse<JournalReadDto>.Ok(await _journals.CreateAsync(dto, CurrentUserId), "Journal created."));

    [HttpGet("journals")]
    public async Task<IActionResult> List([FromQuery] int limit = 200)
        => Ok(ApiResponse<List<JournalReadDto>>.Ok(await _journals.ListAsync(limit)));

    [HttpGet("journals/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var j = await _journals.GetAsync(id);
        return j == null ? NotFound(ApiResponse<object>.Fail("Journal not found.", 404))
                         : Ok(ApiResponse<JournalReadDto>.Ok(j));
    }

    [HttpPost("journals/{id}/submit")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Submit(string id)
        => Ok(ApiResponse<JournalReadDto>.Ok(await _journals.SubmitForReviewAsync(id, CurrentUserId), "Submitted for review."));

    [HttpPost("journals/{id}/review")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Review(string id)
        => Ok(ApiResponse<JournalReadDto>.Ok(await _journals.ReviewAsync(id, CurrentUserId), "Reviewed — awaiting approval."));

    [HttpPost("journals/{id}/approve")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Approve(string id)
        => Ok(ApiResponse<JournalReadDto>.Ok(await _journals.ApproveAndPostAsync(id, CurrentUserId), "Approved and posted."));

    [HttpPost("journals/{id}/reverse")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Reverse(string id)
        => Ok(ApiResponse<JournalReadDto>.Ok(await _journals.ReverseAsync(id, CurrentUserId), "Reversed."));
}

public class ReportsController : BaseFinanceController
{
    private readonly IJournalService _journals;
    public ReportsController(IJournalService journals) => _journals = journals;

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] DateTime? asOf, [FromQuery] string? costCentreId, [FromQuery] string? branchId)
        => Ok(ApiResponse<TrialBalanceDto>.Ok(
            await _journals.GetTrialBalanceAsync(asOf ?? DateTime.UtcNow, costCentreId, branchId)));
}
