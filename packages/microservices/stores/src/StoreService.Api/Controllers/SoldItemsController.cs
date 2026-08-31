using Asp.Versioning;
using StoreService.Core.DTOs.SoldItems;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sold-items")]
public class SoldItemsController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] SoldItemFilterParameters filters)
    {
        var result = await stock.GetSoldItemsAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var sold = await stock.GetSoldItemByIdAsync(id);
        if (sold is null) return NotFoundResult($"Sold item {id} not found.");
        return OkResult(sold);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateSoldItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        try
        {
            var sold = await stock.CreateSoldItemAsync(dto, CurrentUserId);
            return CreatedResult(sold, "Sale recorded.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
