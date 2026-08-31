using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Deviations;
using OperationsService.Core.Entities;
using OperationsService.Infrastructure.Data;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assignments/{assignmentId}/ncrs")]
[Authorize]
public class NonConformanceController(OperationsDbContext db) : ControllerBase
{
    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    private static NonConformanceReportDto Map(NonConformanceReport n) => new()
    {
        Id              = n.Id,
        AssignmentId    = n.AssignmentId,
        AncrNumber      = n.AncrNumber,
        Type            = n.Type,
        Category        = n.Category,
        Description     = n.Description,
        DetectionMethod = n.DetectionMethod,
        ImmediateAction = n.ImmediateAction,
        RootCause       = n.RootCause,
        CorrectiveAction= n.CorrectiveAction,
        PreventiveAction= n.PreventiveAction,
        Status          = n.Status,
        RaisedByName    = n.RaisedByName,
        CreatedAt       = n.CreatedAt,
        ClosedByName    = n.ClosedByName,
        ClosedAt        = n.ClosedAt,
        ClosureNotes    = n.ClosureNotes,
    };

    [HttpGet]
    public async Task<ActionResult<List<NonConformanceReportDto>>> GetAll(string assignmentId)
    {
        var list = await db.NonConformanceReports
            .Where(n => n.AssignmentId == assignmentId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
        return list.Select(Map).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<NonConformanceReportDto>> Create(string assignmentId, [FromBody] CreateNcrDto dto)
    {
        if (!await db.Assignments.AnyAsync(a => a.Id == assignmentId))
            return NotFound("Assignment not found");

        var year  = DateTime.UtcNow.Year;
        var count = await db.NonConformanceReports.CountAsync() + 1;

        var ncr = new NonConformanceReport
        {
            Id              = Guid.NewGuid().ToString(),
            AssignmentId    = assignmentId,
            AncrNumber      = $"ANCR-{year}-{count:D4}",
            Type            = dto.Type,
            Category        = dto.Category,
            Description     = dto.Description,
            DetectionMethod = dto.DetectionMethod,
            ImmediateAction = dto.ImmediateAction,
            RootCause       = dto.RootCause,
            CorrectiveAction= dto.CorrectiveAction,
            PreventiveAction= dto.PreventiveAction,
            Status          = "Open",
            RaisedById      = UserId,
            RaisedByName    = UserName,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        };
        db.NonConformanceReports.Add(ncr);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { assignmentId }, Map(ncr));
    }

    [HttpPut("{ncrId}")]
    public async Task<ActionResult<NonConformanceReportDto>> Update(string assignmentId, string ncrId, [FromBody] UpdateNcrDto dto)
    {
        var ncr = await db.NonConformanceReports.FirstOrDefaultAsync(n => n.Id == ncrId && n.AssignmentId == assignmentId);
        if (ncr is null) return NotFound();
        if (ncr.Status == "Closed") return BadRequest("Cannot update a closed NCR.");

        if (dto.RootCause        != null) ncr.RootCause        = dto.RootCause;
        if (dto.CorrectiveAction != null) ncr.CorrectiveAction = dto.CorrectiveAction;
        if (dto.PreventiveAction != null) ncr.PreventiveAction = dto.PreventiveAction;
        if (dto.Status is "Open" or "UnderInvestigation") ncr.Status = dto.Status;
        ncr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(ncr);
    }

    [HttpPost("{ncrId}/close")]
    public async Task<ActionResult<NonConformanceReportDto>> Close(string assignmentId, string ncrId, [FromBody] CloseNcrDto dto)
    {
        var ncr = await db.NonConformanceReports.FirstOrDefaultAsync(n => n.Id == ncrId && n.AssignmentId == assignmentId);
        if (ncr is null) return NotFound();
        if (ncr.Status == "Closed") return BadRequest("NCR already closed.");

        ncr.Status       = "Closed";
        ncr.ClosedById   = UserId;
        ncr.ClosedByName = UserName;
        ncr.ClosedAt     = DateTime.UtcNow;
        ncr.ClosureNotes = dto.ClosureNotes;
        ncr.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(ncr);
    }
}
