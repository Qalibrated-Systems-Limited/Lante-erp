using Microsoft.AspNetCore.Mvc;
using TicketingService.Api.Authorization;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Service-to-service endpoint for creating an in-app notification for a user, in whichever
/// tenant schema the request is pinned to via the X-Tenant-Schema header (resolved by
/// TenantDbConnectionInterceptor — there's no JWT on a service-to-service call, so the caller
/// must set it explicitly). Used by user-service's platform broadcast feature.
/// </summary>
[ApiController]
[Route("internal/notifications")]
[ServiceKeyAuthorize]
public class InternalNotificationsController(INotificationService notifications, ILogger<InternalNotificationsController> logger)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "userId and message are required." });

        var notificationId = await notifications.SendAsync(request.UserId, request.Type, request.Message);
        logger.LogInformation("Internal notification {NotificationId} created for user {UserId}", notificationId, request.UserId);
        return Ok(new { success = true, notificationId });
    }

    // Polled by user-service's platform admin "who hasn't read this broadcast" view. Scoped by
    // the caller's X-Tenant-Schema header, same as Send above.
    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatus(string id)
    {
        var notification = await notifications.GetByIdAsync(id);
        if (notification == null) return NotFound(new { message = "Notification not found." });
        return Ok(new { notification.IsRead, notification.ReadAt });
    }

    public record SendNotificationRequest(string UserId, string Type, string Message);
}
