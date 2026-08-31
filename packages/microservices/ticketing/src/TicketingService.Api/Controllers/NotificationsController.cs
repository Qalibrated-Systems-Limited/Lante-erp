using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false)
    {
        var items = await notificationService.GetForUserAsync(CurrentUserId, unreadOnly);
        var data = items.Select(n => new
        {
            n.Id,
            n.Type,
            n.Title,
            n.Message,
            n.TicketId,
            n.TicketTitle,
            n.IsRead,
            n.ReadAt,
            n.CreatedAt,
        });
        return Ok(new { data });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var count = await notificationService.GetUnreadCountAsync(CurrentUserId);
        return Ok(new { data = new { count } });
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        await notificationService.MarkReadAsync(id, CurrentUserId);
        return Ok(new { message = "Marked as read." });
    }

    [HttpPatch("{id}/unread")]
    public async Task<IActionResult> MarkUnread(string id)
    {
        await notificationService.MarkUnreadAsync(id, CurrentUserId);
        return Ok(new { message = "Marked as unread." });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await notificationService.MarkAllReadAsync(CurrentUserId);
        return Ok(new { message = "All notifications marked as read." });
    }
}
