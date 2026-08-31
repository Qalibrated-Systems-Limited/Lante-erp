using TicketingService.Core.Entities;

namespace TicketingService.Core.Interfaces.Repositories;

public interface IKnowledgeBaseRepository : IGenericRepository<KnowledgeBaseArticle>
{
    /// D7-4 — published articles matching a free-text query across title/problem/keywords/category.
    Task<IEnumerable<KnowledgeBaseArticle>> SearchAsync(string? query, int take);
}
