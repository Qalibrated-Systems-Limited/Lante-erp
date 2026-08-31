using Asp.Versioning;
using StoreService.Core.DTOs.Locations;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/locations")]
public class LocationsController(IStockService stock) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] LocationFilterParameters filters)
    {
        var result = await stock.GetLocationsAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var location = await stock.GetLocationByIdAsync(id);
        if (location is null) return NotFoundResult($"Location {id} not found.");
        return OkResult(location);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateLocationDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        try
        {
            var location = await stock.CreateLocationAsync(dto, CurrentUserId);
            return CreatedResult(location, "Location created.");
        }
        catch (InvalidOperationException e)
        {
            return BadRequestResult(e.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateLocationDto dto)
    {
        try
        {
            var location = await stock.UpdateLocationAsync(id, dto, CurrentUserId);
            return OkResult(location, "Location updated.");
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
            await stock.DeleteLocationAsync(id, CurrentUserId);
            return OkResult(new { id }, "Location deleted.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
