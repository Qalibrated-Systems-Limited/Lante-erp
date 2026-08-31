using Asp.Versioning;
using StoreService.Api.Services;
using StoreService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/item-photos")]
public class ItemPhotosController(IItemPhotoService itemPhotoService, LocalFileStorageService storage) : BaseController
{
    [HttpGet("item/{itemId}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetByItem(string itemId)
        => OkResult(await itemPhotoService.GetByItemIdAsync(itemId));

    [HttpGet("{id}")]
    [Authorize(Policy = "stores.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var photo = await itemPhotoService.GetByIdAsync(id);
        if (photo is null) return NotFoundResult("Item photo not found.");
        return OkResult(photo);
    }

    [HttpPost("item/{itemId}")]
    [Authorize(Policy = "stores.write")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(string itemId, IFormFile? image, [FromForm] string? caption = null)
    {
        var file = storage.ResolveFile(image);
        if (file is null) return BadRequestResult("No image file provided.");
        var url = await storage.SaveAsync(file, $"items/{itemId}");
        var photo = await itemPhotoService.AddPhotoAsync(itemId, url, caption);
        return CreatedResult(photo);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "stores.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var url = await itemPhotoService.DeletePhotoAsync(id);
        if (url is null) return NotFoundResult("Item photo not found.");
        storage.Delete(url);
        return NoContent();
    }
}
