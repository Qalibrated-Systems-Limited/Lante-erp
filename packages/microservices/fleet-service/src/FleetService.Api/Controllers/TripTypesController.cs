using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.DTOs.Common;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class TripTypesController(ITripTypeService tripTypeService) : ControllerBase
{
    /// <summary>Dual-mode: omit pageNumber/pageSize to get today's unpaged flat-array
    /// response (used by dropdowns elsewhere); pass either to get a PagedResponseDto.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive = null,
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null)
    {
        if (pageNumber.HasValue || pageSize.HasValue)
        {
            var pn = pageNumber ?? 1;
            var ps = pageSize ?? 20;
            var (items, total) = await tripTypeService.GetPagedAsync(pn, ps, isActive);
            return Ok(new { success = true, data = new PagedResponseDto<TripType> { Items = items, PageNumber = pn, PageSize = ps, TotalCount = total } });
        }

        var allItems = isActive == true
            ? await tripTypeService.GetActiveAsync()
            : await tripTypeService.GetAllAsync();
        return Ok(new { success = true, data = allItems });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var tripType = await tripTypeService.GetByIdAsync(id);
        if (tripType == null) return NotFound(new { success = false, message = "Trip type not found" });
        return Ok(new { success = true, data = tripType });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TripType tripType)
    {
        var created = await tripTypeService.CreateAsync(tripType);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] TripType updated)
    {
        var item = await tripTypeService.UpdateDetailsAsync(id, updated.Name, updated.Description, updated.IsActive, updated.Category, updated.EmptyTripOption, updated.MaterialRequirement);
        if (item == null) return NotFound(new { success = false, message = "Trip type not found" });
        return Ok(new { success = true, data = item });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await tripTypeService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Trip type not found" });
        return NoContent();
    }
}
