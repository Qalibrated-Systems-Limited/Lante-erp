using Asp.Versioning;
using ComplianceService.Core.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComplianceService.Api.Controllers;

// Shared file upload for records that carry a document/certificate URL (policies, anti-bribery
// training certificates, board resolution scanned copies). One endpoint + one PVC instead of a
// separate upload path per entity — same pattern as HSE's UploadsController.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-uploads")]
[Authorize(Policy = "compliance.write")]
public class UploadsController(IConfiguration config) : ControllerBase
{
    private static readonly HashSet<string> AllowedFolders = ["policies", "training", "resolutions", "sops"];
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
        var url = $"{publicBaseUrl}/uploads/compliance/{folder}/{fileName}";

        return Ok(ApiResponse<object>.Ok(new { url }, "File uploaded."));
    }
}
