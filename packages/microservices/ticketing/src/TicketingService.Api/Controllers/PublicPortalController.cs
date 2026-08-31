using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Public-facing portal — no authentication required.
/// Clients can submit tickets (complaints, feedback, project inquiries) and track them by reference.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/portal")]
[AllowAnonymous]
[EnableRateLimiting("portal-track")]   // #17/#18: default limit; submits are stricter (below)
public class PublicPortalController(
    ITicketService ticketService,
    ITicketCategoryService categoryService,
    ITicketAttachmentService attachmentService) : ControllerBase
{
    // Fixed category IDs used for portal submissions (must match seeder). D1-5 — realigned to the
    // spec categories: complaints → cat-complaint, everything else → cat-general-enquiry.
    private const string CatComplaint = "cat-complaint";
    private const string CatGeneral   = "cat-general-enquiry";

    [HttpGet("categories")]
    public async Task<IActionResult> GetPublicCategories()
    {
        var all = await categoryService.GetAllAsync();
        var publicCats = all
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Name, c.Description })
            .ToList();
        return Ok(new { data = publicCats });
    }

    [EnableRateLimiting("portal-submit")]
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] PortalSubmissionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))    return BadRequest(new { message = "Your name is required." });
        if (string.IsNullOrWhiteSpace(dto.Email))   return BadRequest(new { message = "Your email address is required." });
        if (string.IsNullOrWhiteSpace(dto.Subject)) return BadRequest(new { message = "Subject is required." });
        if (string.IsNullOrWhiteSpace(dto.Message)) return BadRequest(new { message = "Message is required." });

        var (categoryId, priority, deptId) = dto.Type switch
        {
            PortalSubmissionType.Complaint      => (CatComplaint, TicketPriority.High,   "CRM"),
            PortalSubmissionType.Feedback       => (CatComplaint, TicketPriority.Low,    "CRM"),
            PortalSubmissionType.ProjectInquiry => (CatGeneral,   TicketPriority.Medium, "General"),
            _                                   => (CatGeneral,   TicketPriority.Low,    "General"),
        };

        var description = $"""
            === CLIENT PORTAL SUBMISSION ===
            Name:    {dto.Name.Trim()}
            Email:   {dto.Email.Trim()}
            Company: {(string.IsNullOrWhiteSpace(dto.Company) ? "—" : dto.Company.Trim())}
            Phone:   {(string.IsNullOrWhiteSpace(dto.Phone)   ? "—" : dto.Phone.Trim())}
            Type:    {dto.Type}
            ================================

            {dto.Message.Trim()}
            """;

        var createDto = new CreateTicketDto
        {
            Title        = $"[Portal] {dto.Subject.Trim()}",
            Description  = description,
            CategoryId   = categoryId,
            Priority     = priority,
            Source       = TicketSource.CRM,
            DepartmentId = deptId,
            RequesterEmail = dto.Email.Trim(),   // #1: so we can warn before auto-close
        };

        var ticket    = await ticketService.CreateAsync(createDto, "portal-anonymous");
        var reference = ticket.Reference;
        // D4-1 — the acknowledgement email is now sent centrally by CreateAsync (once, with ack_sent_at).

        return Ok(new
        {
            data = new
            {
                reference,
                ticketId = ticket.Id,
                message  = "Your submission has been received. Use the reference number below to track its progress.",
            }
        });
    }

    /// <summary>
    /// Upload photos for a portal-submitted ticket (max 3 images, 1 MB each).
    /// Called immediately after a successful /submit using the returned ticketId.
    /// </summary>
    [EnableRateLimiting("portal-submit")]
    [HttpPost("attachments/{ticketId}")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachments(string ticketId)
    {
        var files = Request.Form.Files;
        if (files.Count == 0) return BadRequest(new { message = "No files were provided." });
        if (files.Count > 3)  return BadRequest(new { message = "You may upload at most 3 photos." });

        try
        {
            var result = await attachmentService.AddAttachmentsAsync(ticketId, files, "portal-anonymous");
            return Ok(new { data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("track/{reference}/attachments")]
    public async Task<IActionResult> TrackAttachments(string reference)
    {
        var ticket = await ticketService.GetByReferenceAsync(reference);
        if (ticket == null)
            return NotFound(new { message = "No submission found with that reference number." });

        var attachments = await attachmentService.GetByTicketIdAsync(ticket.Id);
        return Ok(new { data = attachments });
    }

    [EnableRateLimiting("portal-submit")]
    [HttpPost("internal/submit")]
    public async Task<IActionResult> SubmitInternal([FromBody] InternalSubmissionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))    return BadRequest(new { message = "Your name is required." });
        if (string.IsNullOrWhiteSpace(dto.WorkEmail))   return BadRequest(new { message = "Your work email is required." });
        if (string.IsNullOrWhiteSpace(dto.Department))  return BadRequest(new { message = "Department is required." });
        if (string.IsNullOrWhiteSpace(dto.Subject))     return BadRequest(new { message = "Subject is required." });
        if (string.IsNullOrWhiteSpace(dto.Description)) return BadRequest(new { message = "Description is required." });

        var priority = dto.Priority switch
        {
            InternalPriority.Low      => TicketPriority.Low,
            InternalPriority.High     => TicketPriority.High,
            InternalPriority.Critical => TicketPriority.Critical,
            _                         => TicketPriority.Medium,
        };

        var description = $"""
            === INTERNAL STAFF SUBMISSION ===
            Name:       {dto.FullName.Trim()}
            Email:      {dto.WorkEmail.Trim()}
            Department: {dto.Department.Trim()}
            Category:   {(string.IsNullOrWhiteSpace(dto.Category) ? "—" : dto.Category.Trim())}
            =================================

            {dto.Description.Trim()}
            """;

        var createDto = new CreateTicketDto
        {
            Title        = $"[Staff] {dto.Subject.Trim()}",
            Description  = description,
            CategoryId   = CatGeneral,
            Priority     = priority,
            Source       = TicketSource.Manual,
            DepartmentId = dto.Department.Trim(),
            RequesterEmail = dto.WorkEmail.Trim(),   // #1: warn before auto-close
        };

        var ticket    = await ticketService.CreateAsync(createDto, "staff-portal-anonymous");
        var reference = ticket.Reference;
        // D4-1 — acknowledgement email now sent centrally by CreateAsync.

        return Ok(new
        {
            data = new
            {
                reference,
                ticketId = ticket.Id,
                message  = "Your ticket has been submitted. Use the reference number to track its status.",
            }
        });
    }

    [EnableRateLimiting("portal-submit")]
    [HttpPost("internal/attachments/{ticketId}")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> UploadInternalAttachments(string ticketId)
    {
        var files = Request.Form.Files;
        if (files.Count == 0) return BadRequest(new { message = "No files were provided." });
        if (files.Count > 5)  return BadRequest(new { message = "You may upload at most 5 photos." });

        try
        {
            var result = await attachmentService.AddAttachmentsAsync(ticketId, files, "staff-portal-anonymous");
            return Ok(new { data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("track/{reference}")]
    public async Task<IActionResult> Track(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length < 6)
            return BadRequest(new { message = "Invalid reference number." });

        var ticket = await ticketService.GetByReferenceAsync(reference);
        if (ticket == null)
            return NotFound(new { message = "No submission found with that reference number." });

        return Ok(new
        {
            data = new
            {
                ticketId        = ticket.Id,
                reference       = ticket.Reference,
                subject         = ticket.Title.Replace("[Portal] ", "").Replace("[Staff] ", ""),
                status          = ticket.StatusLabel,
                priority        = ticket.PriorityLabel,
                category        = ticket.CategoryName ?? "—",
                submittedAt     = ticket.CreatedAt,
                updatedAt       = ticket.UpdatedAt,
                isResolved      = ticket.StatusLabel is "Resolved" or "Closed",
                isInternal      = ticket.Title.StartsWith("[Staff]"),
                resolutionNotes = ticket.StatusLabel is "Resolved" or "Closed"
                    ? ticket.ResolutionNotes
                    : null,
            }
        });
    }

    // ── #1: customer reply loop ──────────────────────────────────────────────
    // The public conversation for a tracked submission (staff public comments + customer replies).
    [HttpGet("track/{reference}/comments")]
    public async Task<IActionResult> TrackComments(string reference)
    {
        var ticket = await ticketService.GetByReferenceAsync(reference);
        if (ticket == null) return NotFound(new { message = "No submission found with that reference number." });
        var comments = await ticketService.GetPublicConversationByReferenceAsync(reference);
        return Ok(new { data = comments });
    }

    // A customer replies to their own ticket from the tracking page (no auth). Adds a public comment
    // and un-pends the ticket so it isn't auto-closed for "no response".
    [HttpPost("track/{reference}/reply")]
    public async Task<IActionResult> TrackReply(string reference, [FromBody] PortalReplyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Please enter a message." });

        var (found, comment) = await ticketService.AddPublicReplyAsync(reference, dto.Message, dto.Name);
        if (!found) return NotFound(new { message = "No submission found with that reference number." });
        return Ok(new { data = comment, message = "Your reply has been added — we'll get back to you." });
    }
}

public class PortalReplyDto
{
    public string Message { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public class PortalSubmissionDto
{
    public string Name     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Phone   { get; set; }
    public PortalSubmissionType Type    { get; set; } = PortalSubmissionType.General;
    public string Subject  { get; set; } = string.Empty;
    public string Message  { get; set; } = string.Empty;
}

public enum PortalSubmissionType
{
    Complaint,
    Feedback,
    ProjectInquiry,
    General,
}

public class InternalSubmissionDto
{
    public string FullName   { get; set; } = string.Empty;
    public string WorkEmail  { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Category   { get; set; } = string.Empty;
    public string Subject    { get; set; } = string.Empty;
    public string Description{ get; set; } = string.Empty;
    public InternalPriority Priority { get; set; } = InternalPriority.Medium;
}

public enum InternalPriority { Low, Medium, High, Critical }
