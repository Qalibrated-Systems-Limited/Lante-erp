using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payment-alerts")]
[Authorize]
public class PaymentAlertsController(IPaymentAlertService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string Schema => User.FindFirstValue("schema") ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> GetAlerts([FromQuery] string? status, [FromQuery] string? type, [FromQuery] string? severity)
        => Ok(new { data = await service.GetAlertsAsync(status, type, severity) });

    [HttpGet("summary")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync(Schema) });

    [HttpPost("{id}/acknowledge")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> Acknowledge(string id)
    {
        var r = await service.AcknowledgeAsync(id, UserId);
        return r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
    }
}
