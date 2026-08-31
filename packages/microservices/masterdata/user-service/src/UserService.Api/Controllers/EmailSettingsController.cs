using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.DTOs.Settings;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

// Per-tenant custom SMTP settings, kept separate from the general SystemSettingsController — that
// registry is readable by any authenticated user, and an SMTP password (even encrypted) has no
// business being in a payload every logged-in user's dashboard fetches.
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system-settings/email")]
public class EmailSettingsController(IEmailSettingsService emailSettingsService) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "settings.manage")]
    public async Task<IActionResult> Get()
    {
        var settings = await emailSettingsService.GetAsync();
        return OkResult(settings);
    }

    [HttpPut]
    [Authorize(Policy = "settings.manage")]
    public async Task<IActionResult> Save([FromBody] SaveEmailSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SmtpHost))
            return BadRequestResult("SMTP host is required.");
        if (dto.SmtpPort is <= 0 or > 65535)
            return BadRequestResult("SMTP port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(dto.FromEmail))
            return BadRequestResult("From email is required.");

        var saved = await emailSettingsService.SaveAsync(dto);
        return OkResult(saved);
    }

    [HttpPost("test")]
    [Authorize(Policy = "settings.manage")]
    public async Task<IActionResult> SendTest([FromBody] SendTestEmailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ToEmail))
            return BadRequestResult("An email address is required to send the test to.");

        try
        {
            await emailSettingsService.SendTestEmailAsync(dto.ToEmail);
            return OkResult(new { sent = true }, "Test email sent — check the inbox.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResult(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequestResult($"SMTP send failed: {ex.Message}");
        }
    }
}
