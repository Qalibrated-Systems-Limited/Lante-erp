using Asp.Versioning;
using StoreService.Core.DTOs.Transfers;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/store-transfers")]
public class StoreTransfersController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] StoreTransferFilterParameters filters)
    {
        var result = await stock.GetTransfersAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var transfer = await stock.GetTransferByIdAsync(id);
        if (transfer is null) return NotFoundResult($"Transfer {id} not found.");
        return OkResult(transfer);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateStoreTransferDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        try
        {
            var transfer = await stock.CreateTransferAsync(dto, CurrentUserId);
            return CreatedResult(transfer, "Transfer requested.");
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

    [HttpPost("{id}/approve")]
    [Authorize(Policy = "stores.approve")]
    public async Task<IActionResult> Approve(string id, [FromBody] ApproveStoreTransferDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("ApprovedBy is required.");
        try
        {
            var transfer = await stock.ApproveTransferAsync(id, dto, CurrentUserId);
            return OkResult(transfer, "Transfer approved — stock moved between locations.");
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

    [HttpPost("{id}/cancel")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Cancel(string id)
    {
        try
        {
            var transfer = await stock.CancelTransferAsync(id, CurrentUserId);
            return OkResult(transfer, "Transfer cancelled.");
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
