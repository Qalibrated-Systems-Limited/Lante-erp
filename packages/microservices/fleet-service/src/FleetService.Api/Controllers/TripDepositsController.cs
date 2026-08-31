using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.DTOs.TripDeposit;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.tripdeposits")]
[ApiController]
[Route("api/v1/[controller]")]
public class TripDepositsController(ITripDepositService depositService, LocalFileStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tripId = null)
        => Ok(new { success = true, data = await depositService.GetAllAsync(tripId) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var deposit = await depositService.GetByIdAsync(id);
        if (deposit == null) return NotFound(new { success = false, message = "Deposit not found" });
        return Ok(new { success = true, data = deposit });
    }

    [HttpGet("trip/{tripId}")]
    public async Task<IActionResult> GetByTrip(string tripId)
        => Ok(new { success = true, data = await depositService.GetByTripIdAsync(tripId) });

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> Create([FromForm] CreateTripDepositDto dto, IFormFile? screenshot)
    {
        var created = await depositService.CreateFromDtoAsync(dto);
        var file = storage.ResolveFile(screenshot);
        if (file != null)
        {
            var url = await storage.SaveAsync(file, $"tripdeposits/{created.Id}");
            created = (await depositService.UpdateScreenshotAsync(created.Id, url)).deposit ?? created;
        }
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/screenshot")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadScreenshot(string id, IFormFile? screenshot)
    {
        var file = storage.ResolveFile(screenshot);
        if (file == null) return BadRequest(new { success = false, message = "No image file provided" });
        var url = await storage.SaveAsync(file, $"tripdeposits/{id}");
        var (deposit, oldUrl) = await depositService.UpdateScreenshotAsync(id, url);
        if (deposit == null) { storage.Delete(url); return NotFound(new { success = false, message = "Deposit not found" }); }
        storage.Delete(oldUrl);
        return Ok(new { success = true, data = new { screenshot = url } });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await depositService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Deposit not found" });
        return NoContent();
    }
}
