using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface ISatisfactionRatingRepository : IGenericRepository<TicketSatisfactionRating>
{
    Task<TicketSatisfactionRating?> GetByTicketIdAsync(string ticketId);
    Task<double> GetAverageRatingAsync(string? categoryId, DateTime? from, DateTime? to);
    /// #14 — all ratings in a date range (newest first) for the CSAT report.
    Task<List<TicketSatisfactionRating>> GetInRangeAsync(DateTime? from, DateTime? to);
}
