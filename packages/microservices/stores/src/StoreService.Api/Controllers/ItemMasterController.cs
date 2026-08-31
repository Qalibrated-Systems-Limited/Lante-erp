using Asp.Versioning;
using StoreService.Core.DTOs.Items;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/items")]
public class ItemMasterController(IPurchasingService purchasing) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] ItemMasterFilterParameters filters)
    {
        var result = await purchasing.GetItemsAsync(filters);
        return OkResult(result);
    }

    [HttpGet("low-stock")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetLowStock()
    {
        var result = await purchasing.GetLowStockItemsAsync();
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await purchasing.GetItemByIdAsync(id);
        if (item is null) return NotFoundResult($"Item {id} not found.");
        return OkResult(item);
    }

    [HttpGet("{id}/price-history")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetPriceHistory(string id)
    {
        var result = await purchasing.GetPriceHistoryForItemAsync(id);
        return OkResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateItemMasterDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var item = await purchasing.CreateItemAsync(dto, CurrentUserId);
        return CreatedResult(item, "Item created.");
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateItemMasterDto dto)
    {
        try
        {
            var item = await purchasing.UpdateItemAsync(id, dto, CurrentUserId);
            return OkResult(item, "Item updated.");
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
            await purchasing.DeleteItemAsync(id, CurrentUserId);
            return OkResult(new { id }, "Item deleted.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }

    [HttpPost("{id}/acknowledge-low-stock")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> AcknowledgeLowStock(string id)
    {
        try
        {
            var item = await purchasing.AcknowledgeLowStockAsync(id, CurrentUserId);
            return OkResult(item, "Low-stock alert acknowledged.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
