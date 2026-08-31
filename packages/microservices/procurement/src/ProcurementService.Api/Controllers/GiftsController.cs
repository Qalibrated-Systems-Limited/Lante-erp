using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementService.Core.DTOs.Suppliers;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Api.Controllers;

/// <summary>P1 (PROC-007, anti-bribery) — the gift &amp; hospitality register. Every gift a staff member
/// receives from a supplier must be declared.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/procurement/gifts")]
[Authorize]
public class GiftsController(ISupplierService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet]
    [Authorize(Policy = "Permission:procurement.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] string? supplierId)
        => Ok(new { data = await service.GetGiftsAsync(supplierId) });

    [HttpPost]
    [Authorize(Policy = "Permission:procurement.write")]
    public async Task<IActionResult> Declare([FromBody] DeclareGiftDto dto)
        => Ok(new { data = await service.DeclareGiftAsync(dto, UserId, UserName) });
}
