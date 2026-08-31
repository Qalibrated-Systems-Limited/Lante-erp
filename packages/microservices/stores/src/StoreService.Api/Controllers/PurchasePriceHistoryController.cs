using Asp.Versioning;
using StoreService.Core.DTOs.PriceHistory;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/purchase-price-history")]
public class PurchasePriceHistoryController(IPurchasingService purchasing) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] PurchasePriceHistoryFilterParameters filters)
    {
        var result = await purchasing.GetPriceHistoryAsync(filters);
        return OkResult(result);
    }

    // No PUT/DELETE — this is an immutable audit trail.
    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreatePurchasePriceHistoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var entry = await purchasing.CreatePriceHistoryAsync(dto, CurrentUserId);
        return CreatedResult(entry, "Purchase price recorded.");
    }
}
