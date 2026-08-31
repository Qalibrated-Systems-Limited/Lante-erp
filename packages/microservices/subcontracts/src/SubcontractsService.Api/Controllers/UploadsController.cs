using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubcontractsService.Core.DTOs.Common;

namespace SubcontractsService.Api.Controllers;

// Shared file upload for records that carry a document URL (PQQ documents, signed subcontract
// agreements). One endpoint + one PVC instead of a separate upload path per entity.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subcontracts-uploads")]
[Authorize(Policy = "subcontracts.write")]
public class UploadsController(IConfiguration config) : ControllerBase
{
    private static readonly HashSet<string> AllowedFolders = ["prequalifications", "awards"];
    private static readonly HashSet<string> AllowedExtensions =
        [".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".webp"];

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ApiResponse<object>>> Upload([FromForm] string folder, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file provided.", 400));

        if (!AllowedFolders.Contains(folder))
            return BadRequest(ApiResponse<object>.Fail($"Unknown folder '{folder}'.", 400));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.Fail(
                $"File type '{ext}' is not allowed. Accepted: pdf, doc, docx, jpg, jpeg, png, webp.", 400));

        var basePath = config["Storage:BasePath"] ?? "/app/uploads";
        var dir = Path.Combine(basePath, folder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        var publicBaseUrl = config["Storage:BaseUrl"]?.TrimEnd('/');
        var url = $"{publicBaseUrl}/uploads/subcontracts/{folder}/{fileName}";

        return Ok(ApiResponse<object>.Ok(new { url }, "File uploaded."));
    }
}
