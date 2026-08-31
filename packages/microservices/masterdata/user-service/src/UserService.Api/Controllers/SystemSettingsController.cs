using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Api.Services;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

// Generic per-tenant settings registry — company identity, branding, finance limits, MSP
// margins, alert windows, banking, etc. Read by any authenticated user (several settings,
// e.g. branding, are shown app-wide); writes are settings.manage only.
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system-settings")]
public class SystemSettingsController(ISystemSettingService settingService, LocalFileStorageService storage) : BaseController
{
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var settings = await settingService.GetAllAsync();
        return OkResult(settings);
    }

    [HttpPut]
    [Authorize(Policy = "settings.manage")]
    public async Task<IActionResult> Update([FromBody] UpdateSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequestResult("key is required.");

        var updated = await settingService.UpsertAsync(request.Key, request.Value ?? string.Empty);
        return OkResult(updated);
    }

    [HttpPost("logo")]
    [Authorize(Policy = "settings.manage")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        file = storage.ResolveFile(file, "file");
        if (file == null || file.Length == 0)
            return BadRequestResult("file is required.");

        if (file.Length > MaxLogoBytes)
            return BadRequestResult("Logo must be 2MB or smaller.");

        string url;
        try
        {
            url = await storage.SaveAsync(file, "branding");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResult(ex.Message);
        }

        await settingService.UpsertAsync("branding.logo_url", url);
        return OkResult(new { url });
    }

    public record UpdateSettingRequest(string Key, string? Value);
}
