using FinanceService.Core.DTOs;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

// ── Statutory remittances (FIN-025) ──
public class StatutoryController : BaseFinanceController
{
    private readonly IStatutoryService _svc;
    public StatutoryController(IStatutoryService svc) => _svc = svc;

    [HttpGet("statutory/obligations")]
    public async Task<IActionResult> Obligations([FromQuery] string period)
        => Ok(ApiResponse<List<ObligationDto>>.Ok(await _svc.GetObligationsAsync(period)));

    [HttpGet("statutory/remittances")]
    public async Task<IActionResult> Remittances()
        => Ok(ApiResponse<List<RemittanceReadDto>>.Ok(await _svc.ListRemittancesAsync()));

    [HttpPost("statutory/remit")]
    [Authorize(Policy = "finance.approve")]
    public async Task<IActionResult> Remit([FromBody] RemitStatutoryDto dto)
        => Ok(ApiResponse<RemittanceReadDto>.Ok(await _svc.RemitAsync(dto, CurrentUserId), "Remittance posted."));
}
