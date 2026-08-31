using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class FeedbackController(IFeedbackService feedbackService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? userId = null, [FromQuery] FleetFeedbackStatus? status = null)
        => Ok(new { success = true, data = await feedbackService.GetFilteredAsync(userId, status) });

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var feedback = await feedbackService.GetByIdAsync(id);
        if (feedback == null) return NotFound(new { success = false, message = "Feedback not found" });
        return Ok(new { success = true, data = feedback });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Feedback feedback)
    {
        feedback.Status = FleetFeedbackStatus.Pending;
        var created = await feedbackService.CreateAsync(feedback);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/respond")]
    public async Task<IActionResult> Respond(string id, [FromBody] FeedbackResponse req)
    {
        var feedback = await feedbackService.RespondAsync(id, req.Response, req.RespondedByUserId, req.Status);
        if (feedback == null) return NotFound(new { success = false, message = "Feedback not found" });
        return Ok(new { success = true, data = feedback });
    }

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await feedbackService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Feedback not found" });
        return NoContent();
    }
}

public record FeedbackResponse(string Response, string RespondedByUserId, FleetFeedbackStatus? Status);
