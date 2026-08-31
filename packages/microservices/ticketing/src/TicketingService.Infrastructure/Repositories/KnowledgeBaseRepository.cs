using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Infrastructure.Data;

namespace TicketingService.Infrastructure.Repositories;

public class KnowledgeBaseRepository(TicketingDbContext context)
    : GenericRepository<KnowledgeBaseArticle>(context), IKnowledgeBaseRepository
{
    public async Task<IEnumerable<KnowledgeBaseArticle>> SearchAsync(string? query, int take)
    {
        var q = Context.KnowledgeBaseArticles.Where(a => a.IsPublished);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.ToLower();
            q = q.Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.Problem.ToLower().Contains(term) ||
                a.Category.ToLower().Contains(term) ||
                (a.Keywords != null && a.Keywords.ToLower().Contains(term)));
        }

        return await q
            .OrderByDescending(a => a.ViewCount)   // most-used first
            .ThenByDescending(a => a.UpdatedAt)
            .Take(take)
            .ToListAsync();
    }
}
