using Asp.Versioning;
using LicenseService.Core.Constants;
using LicenseService.Core.DTOs.Licenses;
using LicenseService.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LicenseService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/licenses")]
public class LicensesController(
    ILicenseService licenseService,
    ILogger<LicensesController> logger)
    : BaseController
{
    // ── PUBLIC: Client app check-in (no auth required — token IS the credential) ──
    [HttpPost("validate")]
    [AllowAnonymous]

    public async Task<IActionResult> Validate([FromBody] ValidateLicenseDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequestResult("Invalid request.");

        var result = await licenseService.ValidateAsync(dto);

        if (!result.Valid)
        {
            logger.LogWarning("License validation rejected: reason={Reason} app={AppId}", result.Reason, dto.AppId);
            return Unauthorized(new { result.Valid, result.Reason });
        }

        return OkResult(result, "License valid.");
    }

    // ── PUBLIC: Available features + app IDs (drives the ERP issue form) ────────
    [HttpGet("metadata")]
    [AllowAnonymous]
    public IActionResult GetMetadata()
    {
        var data = new
        {
            AppIds   = LicenseAppIds.All,
            Features = LicenseFeatures.All
                .GroupBy(f => f.Group.ToString())
                .Select(g => new
                {
                    Group = g.Key,
                    Items = g.Select(f => new { f.Value, f.Label }),
                }),
        };
        return OkResult(data, "License metadata.");
    }

    // ── ADMIN: Issue a new license ────────────────────────────────────────────
    [HttpPost]
    [Authorize(Policy = "licensing.write")]
    public async Task<IActionResult> Issue([FromBody] IssueLicenseDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequestResult("Invalid request data.");

        if (dto.ExpiresAt <= DateTime.UtcNow)
            return BadRequestResult("ExpiresAt must be in the future.");

        // Reject unknown app IDs
        if (!LicenseAppIds.IsValid(dto.AppId))
            return BadRequestResult(
                $"Unknown app ID '{dto.AppId}'. Valid: {string.Join(", ", LicenseAppIds.All)}");

        // Reject unknown feature flags
        var unknown = LicenseFeatures.FindUnknown(dto.Features).ToList();
        if (unknown.Count > 0)
            return BadRequestResult(
                $"Unknown feature(s): {string.Join(", ", unknown)}. " +
                $"Valid: {string.Join(", ", LicenseFeatures.All.Select(f => f.Value))}");

        var issuedBy = User.Identity?.Name ?? "system";
        var result   = await licenseService.IssueAsync(dto, issuedBy);

        return CreatedResult(result, "License issued successfully.");
    }

    // ── ADMIN: Get all licenses with optional filters ─────────────────────────
    [HttpGet]
    [Authorize(Policy = "licensing.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? customerId,
        [FromQuery] string? appId,
        [FromQuery] bool?   active)
    {
        var licenses = await licenseService.GetAllAsync(customerId, appId, active);
        return OkResult(licenses);
    }

    // ── ADMIN: Get single license by ID ──────────────────────────────────────
    [HttpGet("{id}")]
    [Authorize(Policy = "licensing.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var license = await licenseService.GetByIdAsync(id);
        if (license is null) return NotFoundResult($"License {id} not found.");
        return OkResult(license);
    }

    // ── ADMIN: Get licenses expiring within N days ────────────────────────────
    [HttpGet("expiring")]
    [Authorize(Policy = "licensing.read")]
    public async Task<IActionResult> GetExpiring([FromQuery] int withinDays = 30)
    {
        var licenses = await licenseService.GetExpiringAsync(withinDays);
        return OkResult(licenses, $"Licenses expiring within {withinDays} days.");
    }

    // ── ADMIN: Renew a license (extends ExpiresAt; token is unchanged) ───────
    [HttpPost("{id}/renew")]
    [Authorize(Policy = "licensing.write")]
    public async Task<IActionResult> Renew(string id, [FromBody] RenewLicenseDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequestResult("New expiry date is required.");

        if (dto.NewExpiresAt <= DateTime.UtcNow)
            return BadRequestResult("NewExpiresAt must be in the future.");

        try
        {
            var renewedBy = User.Identity?.Name ?? "system";
            var result    = await licenseService.RenewAsync(id, dto.NewExpiresAt, renewedBy);
            return OkResult(result, "License renewed successfully.");
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResult($"License {id} not found.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResult(ex.Message);
        }
    }

    // ── ADMIN: Revoke a license ───────────────────────────────────────────────
    [HttpDelete("{id}")]
    [Authorize(Policy = "licensing.delete")]
    public async Task<IActionResult> Revoke(string id, [FromBody] RevokeLicenseDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequestResult("Revoke reason is required.");

        try
        {
            var revokedBy = User.Identity?.Name ?? "system";
            await licenseService.RevokeAsync(id, dto.Reason, revokedBy);
            return OkResult(new { id }, "License revoked. The client app will detect this within 24 hours.");
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResult($"License {id} not found.");
        }
    }
}
