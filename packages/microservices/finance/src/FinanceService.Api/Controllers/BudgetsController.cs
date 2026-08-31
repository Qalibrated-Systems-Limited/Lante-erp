using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

public class BudgetsController : BaseFinanceController
{
    private readonly IBudgetService _svc;
    public BudgetsController(IBudgetService svc) => _svc = svc;

    [HttpGet("budgets")]
    public async Task<IActionResult> Budgets([FromQuery] string fiscalYearId, [FromQuery] bool includeSuperseded = false)
        => Ok(ApiResponse<List<BudgetReadDto>>.Ok(await _svc.ListBudgetsAsync(fiscalYearId, includeSuperseded)));

    [HttpPost("budgets")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetDto dto)
        => Ok(ApiResponse<BudgetReadDto>.Ok(await _svc.CreateBudgetAsync(dto, CurrentUserId), "Budget saved."));

    [HttpGet("revenue-targets")]
    public async Task<IActionResult> Targets([FromQuery] string fiscalYearId)
        => Ok(ApiResponse<List<RevenueTargetReadDto>>.Ok(await _svc.ListTargetsAsync(fiscalYearId)));

    [HttpPost("revenue-targets")]
    [Authorize(Policy = "finance.write")]
    public async Task<IActionResult> CreateTarget([FromBody] CreateRevenueTargetDto dto)
        => Ok(ApiResponse<RevenueTargetReadDto>.Ok(await _svc.CreateTargetAsync(dto, CurrentUserId), "Revenue target saved."));
}
