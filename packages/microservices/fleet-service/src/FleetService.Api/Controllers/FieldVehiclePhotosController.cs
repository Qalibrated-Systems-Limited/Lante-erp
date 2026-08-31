using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class FieldVehiclePhotosController(IFieldVehiclePhotoService fieldVehiclePhotoService, LocalFileStorageService storage) : ControllerBase
{
    [HttpGet("field-vehicle/{fieldVehicleId}")]
    public async Task<IActionResult> GetByFieldVehicle(string fieldVehicleId)
        => Ok(new { success = true, data = await fieldVehiclePhotoService.GetByFieldVehicleIdAsync(fieldVehicleId) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var photo = await fieldVehiclePhotoService.GetByIdAsync(id);
        if (photo == null) return NotFound(new { success = false, message = "Field vehicle photo not found" });
        return Ok(new { success = true, data = photo });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("field-vehicle/{fieldVehicleId}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(string fieldVehicleId, IFormFile? image, [FromForm] string? caption = null)
    {
        var file = storage.ResolveFile(image);
        if (file == null) return BadRequest(new { success = false, message = "No image file provided" });
        var url = await storage.SaveAsync(file, $"field-vehicles/{fieldVehicleId}");
        var photo = await fieldVehiclePhotoService.AddPhotoAsync(fieldVehicleId, url, caption);
        return CreatedAtAction(nameof(GetById), new { id = photo.Id }, new { success = true, data = photo });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var url = await fieldVehiclePhotoService.DeletePhotoAsync(id);
        if (url == null) return NotFound(new { success = false, message = "Field vehicle photo not found" });
        storage.Delete(url);
        return NoContent();
    }
}
