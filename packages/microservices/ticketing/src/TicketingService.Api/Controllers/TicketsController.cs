using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Complaints;
using TicketingService.Core.DTOs.Satisfaction;
using TicketingService.Core.DTOs.Tickets;
using Microsoft.AspNetCore.Http;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tickets")]
public class TicketsController(ITicketService ticketService, ITicketHistoryService historyService, ISatisfactionService satisfactionService, ITicketAttachmentService attachmentService, IEscalationService escalationService, IComplaintWorkflowService complaintWorkflowService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<TicketReadDto>>>> GetAll([FromQuery] TicketFilterParameters parameters)
    {
        var userPermissions = User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet();

        bool canReadAll  = userPermissions.Contains("system.admin") || userPermissions.Contains("tickets.read.all");
        bool canReadDept = userPermissions.Contains("tickets.read.dept");

        if (!canReadAll)
        {
            if (canReadDept)
            {
                var deptIdsClaim = User.FindFirstValue("department_ids");
                if (!string.IsNullOrEmpty(deptIdsClaim))
                {
                    parameters.DepartmentIds = deptIdsClaim
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                }
                else
                {
                    var deptId = User.FindFirstValue("department_id");
                    if (!string.IsNullOrEmpty(deptId))
                        parameters.DepartmentId = deptId;
                }
            }
            else
            {
                // tickets.read.own — only tickets assigned to or created by this user
                parameters.AssignedToMe = true;
            }
        }

        var result = await ticketService.GetPagedAsync(parameters, CurrentUserId);
        return Ok(ApiResponse<PaginatedResult<TicketReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> GetById(string id)
    {
        var ticket = await ticketService.GetByIdAsync(id);
        if (ticket == null) return NotFound(ApiResponse<TicketReadDto>.Fail("Ticket not found.", 404));
        return Ok(ApiResponse<TicketReadDto>.Ok(ticket));
    }

    [HttpPost]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Create([FromBody] CreateTicketDto dto)
    {
        var created = await ticketService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id, version = "1" },
            ApiResponse<TicketReadDto>.Ok(created, "Ticket created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Update(string id, [FromBody] UpdateTicketDto dto)
    {
        var updated = await ticketService.UpdateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket updated successfully."));
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = "tickets.resolve")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> ChangeStatus(string id, [FromBody] ChangeStatusDto dto)
    {
        var updated = await ticketService.ChangeStatusAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Status updated successfully."));
    }

    [HttpPut("{id}/assign")]
    [Authorize(Policy = "tickets.assign")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Assign(string id, [FromBody] AssignTicketDto dto)
    {
        var updated = await ticketService.AssignAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket assigned successfully."));
    }

    // D2-5 — ownership trail: who has held this ticket, assigned by whom, when, with handover notes.
    [HttpGet("{id}/assignments")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketAssignmentReadDto>>>> GetAssignments(string id)
    {
        var assignments = await ticketService.GetAssignmentsAsync(id);
        return Ok(ApiResponse<IEnumerable<TicketAssignmentReadDto>>.Ok(assignments));
    }

    // D5 — complaint 5-step workflow. Steps auto-created when a complaint-category ticket is raised.
    [HttpGet("{id}/complaint-steps")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ComplaintStepReadDto>>>> GetComplaintSteps(string id)
    {
        var steps = await complaintWorkflowService.GetStepsAsync(id);
        return Ok(ApiResponse<IEnumerable<ComplaintStepReadDto>>.Ok(steps));
    }

    [HttpPost("{id}/complaint-steps/{stepNumber:int}/complete")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ComplaintStepReadDto>>>> CompleteComplaintStep(
        string id, int stepNumber, [FromBody] CompleteComplaintStepDto dto)
    {
        var steps = await complaintWorkflowService.CompleteStepAsync(id, stepNumber, dto, CurrentUserId);
        return Ok(ApiResponse<IEnumerable<ComplaintStepReadDto>>.Ok(steps, "Step completed."));
    }

    // D3-1 — parent/child linking (distinct from dup-merge). Children auto-close with the parent.
    [HttpGet("{id}/children")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketReadDto>>>> GetChildren(string id)
    {
        var children = await ticketService.GetChildrenAsync(id);
        return Ok(ApiResponse<IEnumerable<TicketReadDto>>.Ok(children));
    }

    [HttpPost("{id}/children")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> LinkChild(string id, [FromBody] LinkChildTicketDto dto)
    {
        var child = await ticketService.LinkChildAsync(id, dto.ChildTicketId, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(child, "Ticket linked as a child."));
    }

    [HttpDelete("{id}/parent")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> UnlinkParent(string id)
    {
        var child = await ticketService.UnlinkParentAsync(id, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(child, "Ticket unlinked from its parent."));
    }

    [HttpPut("{id}/resolve")]
    [Authorize(Policy = "tickets.resolve")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Resolve(string id, [FromBody] ResolveTicketDto dto)
    {
        var updated = await ticketService.ResolveAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket resolved successfully."));
    }

    [HttpPut("{id}/close")]
    [Authorize(Policy = "tickets.resolve")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Close(string id)
    {
        var updated = await ticketService.CloseAsync(id, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket closed successfully."));
    }

    [HttpPut("{id}/reopen")]
    [Authorize(Policy = "tickets.resolve")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Reopen(string id)
    {
        var updated = await ticketService.ReopenAsync(id, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket reopened successfully."));
    }

    [HttpPut("{id}/department")]
    [Authorize(Policy = "tickets.assign")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> AssignToDepartment(string id, [FromBody] AssignToDepartmentDto dto)
    {
        var updated = await ticketService.AssignToDepartmentAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket assigned to department successfully."));
    }

    [HttpPut("{id}/escalate")]
    [Authorize(Policy = "tickets.resolve")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Escalate(string id, [FromBody] EscalateTicketDto dto)
    {
        var updated = await ticketService.EscalateAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(updated, "Ticket escalated successfully."));
    }

    // #19 — likely duplicates of this ticket (same requester, still open).
    [HttpGet("{id}/duplicates")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketReadDto>>>> GetDuplicates(string id)
    {
        var dups = await ticketService.FindDuplicatesAsync(id);
        return Ok(ApiResponse<IEnumerable<TicketReadDto>>.Ok(dups));
    }

    // #19 — merge this (duplicate) ticket into a surviving one.
    [HttpPost("{id}/merge")]
    [Authorize(Policy = "tickets.assign")]
    public async Task<ActionResult<ApiResponse<TicketReadDto>>> Merge(string id, [FromBody] MergeTicketDto dto)
    {
        var merged = await ticketService.MergeAsync(id, dto.IntoTicketId, CurrentUserId);
        return Ok(ApiResponse<TicketReadDto>.Ok(merged, "Ticket merged."));
    }

    [HttpPut("{id}/escalations/{escalationId}/acknowledge")]
    [Authorize(Policy = "tickets.resolve")]
    public async Task<ActionResult<ApiResponse<object>>> AcknowledgeEscalation(string id, string escalationId)
    {
        var acknowledged = await escalationService.AcknowledgeAsync(id, escalationId, CurrentUserId);
        if (!acknowledged)
            return NotFound(ApiResponse<object>.Fail("Escalation not found for this ticket.", 404));
        return Ok(ApiResponse<object>.Ok(new { }, "Escalation acknowledged."));
    }

    [HttpPost("{id}/comments")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<CommentReadDto>>> AddComment(string id, [FromBody] CreateCommentDto dto)
    {
        dto.TicketId = id;
        var comment = await ticketService.AddCommentAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<CommentReadDto>.Ok(comment, "Comment added successfully."));
    }

    [HttpGet("{id}/comments")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CommentReadDto>>>> GetComments(string id)
    {
        var comments = await ticketService.GetCommentsAsync(id);
        return Ok(ApiResponse<IEnumerable<CommentReadDto>>.Ok(comments));
    }

    [HttpGet("{id}/history")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketHistoryReadDto>>>> GetHistory(string id)
    {
        var history = await historyService.GetByTicketIdAsync(id);
        return Ok(ApiResponse<IEnumerable<TicketHistoryReadDto>>.Ok(history));
    }

    [HttpGet("{id}/watchers")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<string>>>> GetWatchers(string id)
    {
        var watchers = await ticketService.GetWatcherIdsAsync(id);
        return Ok(ApiResponse<IEnumerable<string>>.Ok(watchers));
    }

    [HttpPost("{id}/watchers/{userId}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<object>>> AddWatcher(string id, string userId)
    {
        await ticketService.AddWatcherAsync(id, userId, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(null!, "Watcher added successfully."));
    }

    [HttpDelete("{id}/watchers/{userId}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveWatcher(string id, string userId)
    {
        await ticketService.RemoveWatcherAsync(id, userId);
        return Ok(ApiResponse<object>.Ok(null!, "Watcher removed."));
    }

    [HttpGet("{id}/escalations")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketEscalationDto>>>> GetEscalations(string id)
    {
        var escalations = await escalationService.GetByTicketIdAsync(id);
        return Ok(ApiResponse<IEnumerable<TicketEscalationDto>>.Ok(escalations));
    }

    [HttpGet("my")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketReadDto>>>> GetMyTickets()
    {
        var tickets = await ticketService.GetMyTicketsAsync(CurrentUserId);
        return Ok(ApiResponse<IEnumerable<TicketReadDto>>.Ok(tickets));
    }

    [HttpGet("created")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketReadDto>>>> GetCreatedByMe()
    {
        var tickets = await ticketService.GetCreatedByMeAsync(CurrentUserId);
        return Ok(ApiResponse<IEnumerable<TicketReadDto>>.Ok(tickets));
    }

    [HttpPost("{id}/rating")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<SatisfactionRatingReadDto>>> SubmitRating(string id, [FromBody] SubmitRatingDto dto)
    {
        var rating = await satisfactionService.SubmitRatingAsync(id, dto, CurrentUserId);
        return Ok(ApiResponse<SatisfactionRatingReadDto>.Ok(rating, "Rating submitted successfully."));
    }

    [HttpGet("{id}/rating")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<SatisfactionRatingReadDto>>> GetRating(string id)
    {
        var rating = await satisfactionService.GetRatingAsync(id);
        if (rating == null) return NotFound(ApiResponse<SatisfactionRatingReadDto>.Fail("No rating found for this ticket.", 404));
        return Ok(ApiResponse<SatisfactionRatingReadDto>.Ok(rating));
    }

    [HttpGet("{id}/attachments")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketAttachmentReadDto>>>> GetAttachments(string id)
    {
        var attachments = await attachmentService.GetByTicketIdAsync(id);
        return Ok(ApiResponse<IEnumerable<TicketAttachmentReadDto>>.Ok(attachments));
    }

    [HttpPost("{id}/attachments")]
    [Authorize(Policy = "tickets.write")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<IEnumerable<TicketAttachmentReadDto>>>> AddAttachments(string id)
    {
        var files = Request.Form.Files;
        if (files.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("No files provided.", 400));
        if (files.Count > 3)
            return BadRequest(ApiResponse<object>.Fail("You may upload at most 3 photos.", 400));

        try
        {
            var result = await attachmentService.AddAttachmentsAsync(id, files, CurrentUserId);
            return Ok(ApiResponse<IEnumerable<TicketAttachmentReadDto>>.Ok(result, "Photos uploaded successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
    }

    [HttpDelete("{id}/attachments/{attachmentId}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAttachment(string id, string attachmentId)
    {
        var deleted = await attachmentService.DeleteAttachmentAsync(attachmentId);
        if (!deleted)
            return NotFound(ApiResponse<object>.Fail("Attachment not found.", 404));
        return Ok(ApiResponse<object>.Ok(new { }, "Attachment deleted."));
    }
}
