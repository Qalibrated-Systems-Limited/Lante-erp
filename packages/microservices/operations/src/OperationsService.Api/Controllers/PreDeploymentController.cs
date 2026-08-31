using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.PreDeployment;
using OperationsService.Core.Entities;
using OperationsService.Infrastructure.Data;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assignments/{assignmentId}/pre-deployment")]
[Authorize]
public class PreDeploymentController(OperationsDbContext db) : ControllerBase
{
    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    private static PreDeploymentChecklistDto Map(PreDeploymentChecklist p) => new()
    {
        Id              = p.Id,
        AssignmentId    = p.AssignmentId,
        Status          = p.Status,
        ChecklistJson   = p.ChecklistJson,
        Notes           = p.Notes,
        SubmittedAt     = p.SubmittedAt,
        SubmittedByName = p.SubmittedByName,
        CreatedAt       = p.CreatedAt,
    };

    // GET — return existing checklist or 404
    [HttpGet]
    public async Task<ActionResult<PreDeploymentChecklistDto>> Get(string assignmentId)
    {
        var checklist = await db.PreDeploymentChecklists
            .FirstOrDefaultAsync(p => p.AssignmentId == assignmentId);
        if (checklist is null) return NotFound();
        return Map(checklist);
    }

    // POST — create or update (save draft)
    [HttpPost]
    public async Task<ActionResult<PreDeploymentChecklistDto>> Save(string assignmentId, [FromBody] SavePreDeploymentDto dto)
    {
        var assignment = await db.Assignments.FindAsync(assignmentId);
        if (assignment is null) return NotFound("Assignment not found");

        var checklist = await db.PreDeploymentChecklists
            .FirstOrDefaultAsync(p => p.AssignmentId == assignmentId);

        if (checklist is null)
        {
            checklist = new PreDeploymentChecklist
            {
                Id           = Guid.NewGuid().ToString(),
                AssignmentId = assignmentId,
                Status       = "Draft",
                CreatedAt    = DateTime.UtcNow,
            };
            db.PreDeploymentChecklists.Add(checklist);
        }

        checklist.ChecklistJson = dto.ChecklistJson;
        checklist.Notes         = dto.Notes;
        checklist.UpdatedAt     = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(checklist);
    }

    // POST /submit — finalise the checklist
    [HttpPost("submit")]
    public async Task<ActionResult<PreDeploymentChecklistDto>> Submit(string assignmentId, [FromBody] SubmitPreDeploymentDto dto)
    {
        var assignment = await db.Assignments.FindAsync(assignmentId);
        if (assignment is null) return NotFound("Assignment not found");

        var checklist = await db.PreDeploymentChecklists
            .FirstOrDefaultAsync(p => p.AssignmentId == assignmentId);

        if (checklist is null)
        {
            checklist = new PreDeploymentChecklist
            {
                Id           = Guid.NewGuid().ToString(),
                AssignmentId = assignmentId,
                CreatedAt    = DateTime.UtcNow,
            };
            db.PreDeploymentChecklists.Add(checklist);
        }

        checklist.ChecklistJson   = dto.ChecklistJson;
        checklist.Notes           = dto.Notes;
        checklist.Status          = "Submitted";
        checklist.SubmittedAt     = DateTime.UtcNow;
        checklist.SubmittedById   = UserId;
        checklist.SubmittedByName = UserName;
        checklist.UpdatedAt       = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(checklist);
    }
}
