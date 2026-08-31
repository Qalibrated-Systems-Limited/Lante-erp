using Asp.Versioning;
using StoreService.Core.DTOs.Movements;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-movements")]
public class StockMovementsController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] StockMovementFilterParameters filters)
    {
        var result = await stock.GetMovementsAsync(filters);
        return OkResult(result);
    }

    [HttpGet("balances/{itemId}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetBalancesForItem(string itemId)
    {
        try
        {
            var result = await stock.GetBalancesForItemAsync(itemId);
            return OkResult(result);
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
