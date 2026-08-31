using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class MaterialVariantPhotosController(IMaterialVariantPhotoService variantPhotoService, LocalFileStorageService storage) : ControllerBase
{
    [HttpGet("variant/{variantId}")]
    public async Task<IActionResult> GetByVariant(string variantId)
        => Ok(new { success = true, data = await variantPhotoService.GetByVariantIdAsync(variantId) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var photo = await variantPhotoService.GetByIdAsync(id);
        if (photo == null) return NotFound(new { success = false, message = "Variant photo not found" });
        return Ok(new { success = true, data = photo });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("variant/{variantId}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(string variantId, IFormFile? image, [FromForm] string? caption = null)
    {
        var file = storage.ResolveFile(image);
        if (file == null) return BadRequest(new { success = false, message = "No image file provided" });
        var url = await storage.SaveAsync(file, $"materials/variants/{variantId}");
        var photo = await variantPhotoService.AddPhotoAsync(variantId, url, caption);
        return CreatedAtAction(nameof(GetById), new { id = photo.Id }, new { success = true, data = photo });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var url = await variantPhotoService.DeletePhotoAsync(id);
        if (url == null) return NotFound(new { success = false, message = "Variant photo not found" });
        storage.Delete(url);
        return NoContent();
    }
}
