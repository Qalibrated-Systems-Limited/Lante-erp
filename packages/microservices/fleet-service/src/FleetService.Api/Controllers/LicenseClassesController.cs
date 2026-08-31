using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class LicenseClassesController(ILicenseClassService licenseClassService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(new { success = true, data = await licenseClassService.GetAllAsync() });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var item = await licenseClassService.GetByIdAsync(id);
        if (item == null) return NotFound(new { success = false, message = "License class not found" });
        return Ok(new { success = true, data = item });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LicenseClass licenseClass)
    {
        var created = await licenseClassService.CreateAsync(licenseClass);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] LicenseClass updated)
    {
        var item = await licenseClassService.UpdateDetailsAsync(id, updated.Name, updated.Description);
        if (item == null) return NotFound(new { success = false, message = "License class not found" });
        return Ok(new { success = true, data = item });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await licenseClassService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "License class not found" });
        return NoContent();
    }
}
