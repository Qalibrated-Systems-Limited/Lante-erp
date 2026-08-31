using Microsoft.AspNetCore.Mvc;
using TicketingService.Api.Authorization;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Service-to-service endpoint: the central place any service sends a plain email through,
/// rather than each service standing up its own SMTP client — same pattern as
/// InternalAlertsController/InternalNotificationsController. First consumer is ReportingService's
/// scheduled report delivery (the caller embeds any download link directly in bodyHtml).
/// </summary>
[ApiController]
[Route("internal/email")]
[ServiceKeyAuthorize]
public class InternalEmailController(IPortalEmailService emailService, ILogger<InternalEmailController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest request)
    {
        if (request.To == null || request.To.Length == 0)
            return BadRequest(new { message = "to must contain at least one email address." });
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.BodyHtml))
            return BadRequest(new { message = "subject and bodyHtml are required." });

        await emailService.SendAsync(request.To, request.Subject, request.BodyHtml);

        logger.LogInformation("Internal email sent: \"{Subject}\" to {Count} recipient(s)", request.Subject, request.To.Length);
        return Ok(new { success = true });
    }

    public record SendEmailRequest(string[] To, string Subject, string BodyHtml);
}
