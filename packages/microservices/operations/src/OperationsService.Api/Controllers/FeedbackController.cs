using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Feedback;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Infrastructure.Data;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class FeedbackController(OperationsDbContext db) : ControllerBase
{
    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    // ── Risk Register ──────────────────────────────────────────────────────────

    [HttpGet("api/v{version:apiVersion}/assignments/{assignmentId}/risks")]
    public async Task<ActionResult<List<RiskEntryDto>>> GetRisks(string assignmentId)
    {
        var list = await db.RiskEntries
            .Where(r => r.AssignmentId == assignmentId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
        return list.Select(MapRisk).ToList();
    }

    [HttpPost("api/v{version:apiVersion}/assignments/{assignmentId}/risks")]
    public async Task<ActionResult<RiskEntryDto>> CreateRisk(string assignmentId, [FromBody] CreateRiskDto dto)
    {
        if (!await db.Assignments.AnyAsync(a => a.Id == assignmentId))
            return NotFound("Assignment not found");

        var risk = new RiskEntry
        {
            Id           = Guid.NewGuid().ToString(),
            AssignmentId = assignmentId,
            Title        = dto.Description,
            Description  = dto.Description,
            Likelihood   = ParseOr(dto.Likelihood, RiskLikelihood.Low),
            Impact       = ParseOr(dto.Impact, RiskImpact.Low),
            Score        = RiskEntry.ScoreOf(ParseOr(dto.Likelihood, RiskLikelihood.Low), ParseOr(dto.Impact, RiskImpact.Low)),
            Mitigation   = dto.Mitigation,
            Owner        = dto.Owner,
            Status       = RiskStatus.Open,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        db.RiskEntries.Add(risk);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRisks), new { assignmentId }, MapRisk(risk));
    }

    [HttpPut("api/v{version:apiVersion}/assignments/{assignmentId}/risks/{riskId}")]
    public async Task<ActionResult<RiskEntryDto>> UpdateRisk(string assignmentId, string riskId, [FromBody] UpdateRiskDto dto)
    {
        var risk = await db.RiskEntries.FirstOrDefaultAsync(r => r.Id == riskId && r.AssignmentId == assignmentId);
        if (risk is null) return NotFound();
        if (dto.Mitigation != null) risk.Mitigation = dto.Mitigation;
        if (dto.Owner      != null) risk.Owner      = dto.Owner;
        if (dto.Status is "Open" or "Mitigated" or "Closed" or "Mitigating")
            risk.Status = ParseOr(dto.Status, risk.Status);
        risk.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return MapRisk(risk);
    }

    [HttpDelete("api/v{version:apiVersion}/assignments/{assignmentId}/risks/{riskId}")]
    public async Task<IActionResult> DeleteRisk(string assignmentId, string riskId)
    {
        var risk = await db.RiskEntries.FirstOrDefaultAsync(r => r.Id == riskId && r.AssignmentId == assignmentId);
        if (risk is null) return NotFound();
        risk.IsDeleted = true;
        risk.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Customer Feedback ──────────────────────────────────────────────────────

    [HttpGet("api/v{version:apiVersion}/assignments/{assignmentId}/feedback")]
    public async Task<ActionResult<CustomerFeedbackDto?>> GetFeedback(string assignmentId)
    {
        var fb = await db.CustomerFeedbacks.FirstOrDefaultAsync(f => f.AssignmentId == assignmentId);
        if (fb is null) return NotFound();
        return MapFeedback(fb);
    }

    [HttpPost("api/v{version:apiVersion}/assignments/{assignmentId}/feedback")]
    public async Task<ActionResult<CustomerFeedbackDto>> SaveFeedback(string assignmentId, [FromBody] CreateFeedbackDto dto)
    {
        if (!await db.Assignments.AnyAsync(a => a.Id == assignmentId))
            return NotFound("Assignment not found");

        var fb = await db.CustomerFeedbacks.FirstOrDefaultAsync(f => f.AssignmentId == assignmentId);
        if (fb is null)
        {
            fb = new CustomerFeedback
            {
                Id           = Guid.NewGuid().ToString(),
                AssignmentId = assignmentId,
                CreatedAt    = DateTime.UtcNow,
            };
            db.CustomerFeedbacks.Add(fb);
        }

        fb.OverallRating          = dto.OverallRating;
        fb.TimelinessRating       = dto.TimelinessRating;
        fb.QualityRating          = dto.QualityRating;
        fb.ProfessionalismRating  = dto.ProfessionalismRating;
        fb.WouldRecommend         = dto.WouldRecommend;
        fb.Comments               = dto.Comments;
        fb.CapturedById           = UserId;
        fb.CapturedByName         = UserName;
        fb.UpdatedAt              = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return MapFeedback(fb);
    }

    private static TEnum ParseOr<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static RiskEntryDto MapRisk(RiskEntry r) => new()
    {
        Id          = r.Id,
        AssignmentId= r.AssignmentId ?? string.Empty,
        Description = r.Description,
        Likelihood  = r.Likelihood.ToString(),
        Impact      = r.Impact.ToString(),
        Mitigation  = r.Mitigation,
        Owner       = r.Owner,
        Status      = r.Status.ToString(),
        CreatedAt   = r.CreatedAt,
    };

    private static CustomerFeedbackDto MapFeedback(CustomerFeedback f) => new()
    {
        Id                   = f.Id,
        AssignmentId         = f.AssignmentId,
        OverallRating        = f.OverallRating,
        TimelinessRating     = f.TimelinessRating,
        QualityRating        = f.QualityRating,
        ProfessionalismRating= f.ProfessionalismRating,
        WouldRecommend       = f.WouldRecommend,
        Comments             = f.Comments,
        CapturedByName       = f.CapturedByName,
        CreatedAt            = f.CreatedAt,
    };
}
