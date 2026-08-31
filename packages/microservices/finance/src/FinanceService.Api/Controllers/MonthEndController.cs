using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

public class MonthEndController : BaseFinanceController
{
    private readonly IMonthEndService _svc;
    public MonthEndController(IMonthEndService svc) => _svc = svc;

    [HttpGet("month-end/{periodId}")]
    public async Task<IActionResult> GetClose(string periodId)
        => Ok(ApiResponse<PeriodCloseDto>.Ok(await _svc.GetCloseAsync(periodId)));

    [HttpPost("month-end/checklist/{itemId}")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> Toggle(string itemId, [FromQuery] bool complete = true)
        => Ok(ApiResponse<PeriodCloseDto>.Ok(await _svc.ToggleItemAsync(itemId, complete, CurrentUserId)));

    [HttpPost("month-end/{periodId}/close")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Close(string periodId)
        => Ok(ApiResponse<PeriodCloseDto>.Ok(await _svc.CloseAsync(periodId, CurrentUserId), "Period closed & locked."));

    [HttpPost("month-end/{periodId}/reopen")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Reopen(string periodId)
        => Ok(ApiResponse<PeriodCloseDto>.Ok(await _svc.ReopenAsync(periodId, CurrentUserId), "Period reopened."));

    [HttpGet("month-end/{periodId}/pnl")]
    public async Task<IActionResult> Pnl(string periodId)
        => Ok(ApiResponse<ProfitLossDto>.Ok(await _svc.ProfitAndLossAsync(periodId)));
}
