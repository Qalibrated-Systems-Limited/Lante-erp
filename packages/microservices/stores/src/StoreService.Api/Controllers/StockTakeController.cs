using Asp.Versioning;
using StoreService.Core.DTOs.StockTake;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-take")]
public class StockTakeController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] StockTakeFilterParameters filters)
    {
        var result = await stock.GetStockTakesAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var take = await stock.GetStockTakeByIdAsync(id);
        if (take is null) return NotFoundResult($"Stock take {id} not found.");
        return OkResult(take);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateStockTakeDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        try
        {
            var take = await stock.CreateStockTakeAsync(dto, CurrentUserId);
            return CreatedResult(take, "Stock take recorded.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Policy = "stores.approve")]
    public async Task<IActionResult> Approve(string id, [FromBody] ApproveStockTakeDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("ApprovedBy is required.");
        try
        {
            var take = await stock.ApproveStockTakeAsync(id, dto, CurrentUserId);
            return OkResult(take, "Stock take approved — stock levels adjusted.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return BadRequestResult(e.Message);
        }
    }
}
