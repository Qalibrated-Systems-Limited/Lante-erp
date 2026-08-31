using Asp.Versioning;
using StoreService.Core.DTOs.StockUnits;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-units")]
public class StockUnitsController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] StockUnitFilterParameters filters)
    {
        var result = await stock.GetStockUnitsAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var unit = await stock.GetStockUnitByIdAsync(id);
        if (unit is null) return NotFoundResult($"Stock unit {id} not found.");
        return OkResult(unit);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateStockUnitDto dto)
    {
        try
        {
            var unit = await stock.UpdateStockUnitAsync(id, dto, CurrentUserId);
            return OkResult(unit, "Stock unit updated.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "stores.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await stock.DeleteStockUnitAsync(id, CurrentUserId);
            return OkResult(new { id }, "Stock unit written off.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
