using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

public class CashFlowController : BaseFinanceController
{
    private readonly ICashFlowService _svc;
    public CashFlowController(ICashFlowService svc) => _svc = svc;

    [HttpGet("cash-flow")]
    public async Task<IActionResult> Forecast([FromQuery] DateTime? asOf, [FromQuery] int weeks = 8)
        => Ok(ApiResponse<CashFlowDto>.Ok(await _svc.ForecastAsync(asOf ?? DateTime.UtcNow, weeks)));
}
