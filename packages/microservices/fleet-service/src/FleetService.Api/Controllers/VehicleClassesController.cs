using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class VehicleClassesController(IVehicleClassService vehicleClassService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new { success = true, data = await vehicleClassService.GetAllAsync() });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var vehicleClass = await vehicleClassService.GetByIdAsync(id);
        if (vehicleClass == null) return NotFound(new { success = false, message = "Vehicle class not found" });
        return Ok(new { success = true, data = vehicleClass });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVehicleClassRequest req)
    {
        var vehicleClass = new VehicleClass { Name = req.Name, Description = req.Description };
        var created = await vehicleClassService.CreateAsync(vehicleClass);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateVehicleClassRequest req)
    {
        var vehicleClass = await vehicleClassService.UpdateDetailsAsync(id, req.Name, req.Description);
        if (vehicleClass == null) return NotFound(new { success = false, message = "Vehicle class not found" });
        return Ok(new { success = true, data = vehicleClass });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await vehicleClassService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Vehicle class not found" });
        return NoContent();
    }
}

public record CreateVehicleClassRequest(string Name, string? Description);
