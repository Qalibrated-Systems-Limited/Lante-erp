using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class SatisfactionRatingRepository(TicketingDbContext dbContext)
    : GenericRepository<TicketSatisfactionRating>(dbContext), ISatisfactionRatingRepository
{
    public Task<TicketSatisfactionRating?> GetByTicketIdAsync(string ticketId) =>
        DbSet.FirstOrDefaultAsync(r => r.TicketId == ticketId);

    public async Task<List<TicketSatisfactionRating>> GetInRangeAsync(DateTime? from, DateTime? to)
    {
        var q = DbSet.Include(r => r.Ticket).AsQueryable();
        if (from.HasValue) q = q.Where(r => r.SubmittedAt >= from.Value);
        if (to.HasValue) q = q.Where(r => r.SubmittedAt <= to.Value);
        return await q.OrderByDescending(r => r.SubmittedAt).ToListAsync();
    }

    public async Task<double> GetAverageRatingAsync(string? categoryId, DateTime? from, DateTime? to)
    {
        var query = DbSet.AsQueryable();

        if (from.HasValue) query = query.Where(r => r.SubmittedAt >= from.Value);
        if (to.HasValue) query = query.Where(r => r.SubmittedAt <= to.Value);

        if (categoryId != null)
        {
            query = query.Join(
                Context.Tickets,
                r => r.TicketId,
                t => t.Id,
                (r, t) => new { r, t })
                .Where(x => x.t.CategoryId == categoryId)
                .Select(x => x.r);
        }

        var ratings = await query.Select(r => (double)r.Rating).ToListAsync();
        return ratings.Count == 0 ? 0 : ratings.Average();
    }
}
