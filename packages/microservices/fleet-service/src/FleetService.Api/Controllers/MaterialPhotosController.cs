using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class MaterialPhotosController(IMaterialPhotoService materialPhotoService, LocalFileStorageService storage) : ControllerBase
{
    [HttpGet("material/{materialId}")]
    public async Task<IActionResult> GetByMaterial(string materialId)
        => Ok(new { success = true, data = await materialPhotoService.GetByMaterialIdAsync(materialId) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var photo = await materialPhotoService.GetByIdAsync(id);
        if (photo == null) return NotFound(new { success = false, message = "Material photo not found" });
        return Ok(new { success = true, data = photo });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("material/{materialId}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(string materialId, IFormFile? image, [FromForm] string? caption = null)
    {
        var file = storage.ResolveFile(image);
        if (file == null) return BadRequest(new { success = false, message = "No image file provided" });
        var url = await storage.SaveAsync(file, $"materials/{materialId}");
        var photo = await materialPhotoService.AddPhotoAsync(materialId, url, caption);
        return CreatedAtAction(nameof(GetById), new { id = photo.Id }, new { success = true, data = photo });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var url = await materialPhotoService.DeletePhotoAsync(id);
        if (url == null) return NotFound(new { success = false, message = "Material photo not found" });
        storage.Delete(url);
        return NoContent();
    }
}
