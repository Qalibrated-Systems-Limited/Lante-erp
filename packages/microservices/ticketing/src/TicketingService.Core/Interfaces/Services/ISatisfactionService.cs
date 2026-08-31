using TicketingService.Core.DTOs.Satisfaction;

namespace TicketingService.Core.Interfaces.Services;

public interface ISatisfactionService
{
    Task<SatisfactionRatingReadDto> SubmitRatingAsync(string ticketId, SubmitRatingDto dto, string submittedByUserId);
    Task<SatisfactionRatingReadDto?> GetRatingAsync(string ticketId);
    Task<double> GetAverageRatingAsync(string? categoryId, DateTime? from, DateTime? to);
    /// #14 — aggregate CSAT report over a date range.
    Task<CsatSummaryDto> GetCsatSummaryAsync(DateTime? from, DateTime? to);

    /// D6-3 — customer-service dashboard: open-by-category+aging, SLA compliance, avg resolution time,
    /// avg satisfaction, and survey response rate.
    Task<CsDashboardDto> GetCsDashboardAsync(DateTime? from, DateTime? to);
}
