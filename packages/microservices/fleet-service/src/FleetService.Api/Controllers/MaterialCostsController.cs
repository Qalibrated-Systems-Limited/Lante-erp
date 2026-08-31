using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.expenses")]
[ApiController]
[Route("api/v1/[controller]")]
public class MaterialCostsController(IMaterialCostService materialCostService) : ControllerBase
{
    [HttpGet("material/{materialId}")]
    public async Task<IActionResult> GetByMaterial(string materialId)
        => Ok(new { success = true, data = await materialCostService.GetByMaterialIdAsync(materialId) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var cost = await materialCostService.GetByIdAsync(id);
        if (cost == null) return NotFound(new { success = false, message = "Material cost not found" });
        return Ok(new { success = true, data = cost });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MaterialCost cost)
    {
        var created = await materialCostService.CreateAsync(cost);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] MaterialCost updated)
    {
        var cost = await materialCostService.UpdateCostAsync(id, updated.Cost, updated.Location);
        if (cost == null) return NotFound(new { success = false, message = "Material cost not found" });
        return Ok(new { success = true, data = cost });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await materialCostService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Material cost not found" });
        return NoContent();
    }
}
