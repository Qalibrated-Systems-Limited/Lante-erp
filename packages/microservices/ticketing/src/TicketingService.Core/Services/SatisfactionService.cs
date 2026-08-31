using AutoMapper;
using Microsoft.AspNetCore.Http;
using TicketingService.Core.DTOs.Satisfaction;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class SatisfactionService(
    ISatisfactionRatingRepository ratingRepository,
    ITicketRepository ticketRepository,
    ITicketCategoryRepository categoryRepository,
    ISLAService slaService,
    IAlertService alertService,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper) : ISatisfactionService
{
    private string CurrentSchema =>
        httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value
        ?? httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Schema"].FirstOrDefault()
        ?? string.Empty;

    public async Task<SatisfactionRatingReadDto> SubmitRatingAsync(string ticketId, SubmitRatingDto dto, string submittedByUserId)
    {
        if (dto.Rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");

        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        if (ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed)
            throw new InvalidOperationException("Satisfaction ratings can only be submitted for resolved or closed tickets.");

        var existing = await ratingRepository.GetByTicketIdAsync(ticketId);
        if (existing != null)
            throw new InvalidOperationException("A satisfaction rating has already been submitted for this ticket.");

        var rating = new TicketSatisfactionRating
        {
            TicketId = ticketId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            SubmittedByUserId = submittedByUserId,
            SubmittedAt = DateTime.UtcNow,
            CreatedBy = submittedByUserId
        };

        var created = await ratingRepository.CreateAsync(rating);

        // D6-2 — a low score (< 3) needs the Account Owner (assignee) + Head of BD to intervene.
        if (dto.Rating < 3)
        {
            try
            {
                await alertService.CreateAsync(
                    CurrentSchema, source: "LowSatisfaction", severity: "Warning",
                    title: $"Low satisfaction ({dto.Rating}★) — {ticket.Title}",
                    message: $"Ticket \"{ticket.Title}\" ({ticket.Reference}) was rated {dto.Rating}/5" +
                             (string.IsNullOrWhiteSpace(dto.Comment) ? "." : $": \"{dto.Comment}\".") +
                             " Follow up with the client.",
                    ticketId: ticket.Id, ticketTitle: ticket.Title,
                    assignedToUserId: ticket.AssignedToUserId,   // account owner
                    requiredPermission: "tickets.assign");       // Head of BD / managers
            }
            catch
            {
                // Non-fatal — the rating is saved regardless.
            }
        }

        return mapper.Map<SatisfactionRatingReadDto>(created);
    }

    public async Task<SatisfactionRatingReadDto?> GetRatingAsync(string ticketId)
    {
        var rating = await ratingRepository.GetByTicketIdAsync(ticketId);
        return rating == null ? null : mapper.Map<SatisfactionRatingReadDto>(rating);
    }

    public async Task<CsDashboardDto> GetCsDashboardAsync(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var allTickets = (await ticketRepository.GetAllAsync()).ToList();
        var categories = (await categoryRepository.GetAllAsync())
            .ToDictionary(c => c.Id, c => c.Name);

        // Open-by-category (+ how many have aged past 72 active hours).
        var open = allTickets
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .ToList();
        var openByCategory = open
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryOpenDto
            {
                CategoryId   = g.Key,
                CategoryName = categories.TryGetValue(g.Key, out var n) ? n : "—",
                Open         = g.Count(),
                AgingOver72h = g.Count(t => (now - t.CreatedAt).TotalHours - t.SlaPausedHours > 72),
            })
            .OrderByDescending(c => c.Open)
            .ToList();

        // Avg resolution time over tickets resolved within the range.
        var resolved = allTickets
            .Where(t => t.ResolvedAt.HasValue)
            .Where(t => (!from.HasValue || t.ResolvedAt >= from.Value) && (!to.HasValue || t.ResolvedAt <= to.Value))
            .ToList();
        var avgResolutionHours = resolved.Count == 0
            ? 0
            : Math.Round(resolved.Average(t => Math.Max(0, (t.ResolvedAt!.Value - t.CreatedAt).TotalHours - t.SlaPausedHours)), 1);

        var sla = await slaService.GetSLASummaryAsync();
        var ratings = await ratingRepository.GetInRangeAsync(from, to);
        var surveysSent = await ticketRepository.CountSurveysSentAsync(from, to);

        return new CsDashboardDto
        {
            OpenTotal          = open.Count,
            OpenByCategory     = openByCategory,
            SlaCompliancePct   = sla.ComplianceRate,
            AvgResolutionHours = avgResolutionHours,
            AvgSatisfaction    = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(r => (double)r.Rating), 2),
            SurveysSent        = surveysSent,
            SurveyResponses    = ratings.Count,
            ResponseRatePct    = surveysSent == 0 ? 0 : Math.Round(100.0 * ratings.Count / surveysSent, 1),
        };
    }

    public Task<double> GetAverageRatingAsync(string? categoryId, DateTime? from, DateTime? to) =>
        ratingRepository.GetAverageRatingAsync(categoryId, from, to);

    public async Task<CsatSummaryDto> GetCsatSummaryAsync(DateTime? from, DateTime? to)
    {
        var ratings = await ratingRepository.GetInRangeAsync(from, to);
        var total = ratings.Count;
        var surveysSent = await ticketRepository.CountSurveysSentAsync(from, to);   // D6-1
        return new CsatSummaryDto
        {
            SurveysSent    = surveysSent,
            ResponseRatePct = surveysSent == 0 ? 0 : Math.Round(100.0 * total / surveysSent, 1),
            TotalResponses = total,
            AverageRating  = total == 0 ? 0 : Math.Round(ratings.Average(r => (double)r.Rating), 2),
            // CSAT = share of responses rated 4 or 5.
            CsatPercent    = total == 0 ? 0 : Math.Round(100.0 * ratings.Count(r => r.Rating >= 4) / total, 1),
            Distribution   = Enumerable.Range(1, 5).ToDictionary(s => s, s => ratings.Count(r => r.Rating == s)),
            RecentComments = ratings
                .Take(10)   // most recent responses (newest first), comment optional
                .Select(r => new CsatCommentDto
                {
                    TicketId = r.TicketId,
                    Title = r.Ticket != null ? r.Ticket.Title : string.Empty,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    SubmittedAt = r.SubmittedAt,
                })
                .ToList(),
        };
    }
}
