using Asp.Versioning;
using StoreService.Core.DTOs.UnitsOfMeasure;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/units-of-measure")]
public class UnitsOfMeasureController(IPurchasingService purchasing) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] UnitOfMeasureFilterParameters filters)
    {
        var result = await purchasing.GetUnitsOfMeasureAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var unitOfMeasure = await purchasing.GetUnitOfMeasureByIdAsync(id);
        if (unitOfMeasure is null) return NotFoundResult($"Unit of Measure {id} not found.");
        return OkResult(unitOfMeasure);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var unitOfMeasure = await purchasing.CreateUnitOfMeasureAsync(dto, CurrentUserId);
        return CreatedResult(unitOfMeasure, "Unit of Measure created.");
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUnitOfMeasureDto dto)
    {
        try
        {
            var unitOfMeasure = await purchasing.UpdateUnitOfMeasureAsync(id, dto, CurrentUserId);
            return OkResult(unitOfMeasure, "Unit of Measure updated.");
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
            await purchasing.DeleteUnitOfMeasureAsync(id, CurrentUserId);
            return OkResult(new { id }, "Unit of Measure deleted.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }
}
