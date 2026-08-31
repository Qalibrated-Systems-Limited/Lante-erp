using Asp.Versioning;
using StoreService.Core.DTOs.Grn;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/grn")]
public class GrnController(IPurchasingService purchasing) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetAll([FromQuery] GrnFilterParameters filters)
    {
        var result = await purchasing.GetGrnsAsync(filters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var grn = await purchasing.GetGrnByIdAsync(id);
        if (grn is null) return NotFoundResult($"GRN {id} not found.");
        return OkResult(grn);
    }

    [HttpPost]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Create([FromBody] CreateGrnDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        try
        {
            var grn = await purchasing.CreateGrnAsync(dto, CurrentUserId);
            return CreatedResult(grn, "GRN created.");
        }
        catch (KeyNotFoundException e)
        {
            return NotFoundResult(e.Message);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateGrnDto dto)
    {
        try
        {
            var grn = await purchasing.UpdateGrnAsync(id, dto, CurrentUserId);
            return OkResult(grn, "GRN updated.");
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

    [HttpPost("{id}/inspect")]
    [Authorize(Policy = "stores.write")]
    public async Task<IActionResult> Inspect(string id, [FromBody] InspectGrnDto dto)
    {
        try
        {
            var grn = await purchasing.InspectGrnAsync(id, dto, CurrentUserId);
            return OkResult(grn, dto.Passed ? "GRN passed inspection." : "GRN failed inspection.");
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
