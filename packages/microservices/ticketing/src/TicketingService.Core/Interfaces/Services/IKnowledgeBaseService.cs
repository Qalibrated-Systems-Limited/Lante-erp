using TicketingService.Core.DTOs.KnowledgeBase;

namespace TicketingService.Core.Interfaces.Services;

/// D7-3/D7-4 — knowledge-base articles + deflection search.
public interface IKnowledgeBaseService
{
    Task<IEnumerable<KbArticleReadDto>> GetAllAsync();
    /// Fetch one article and increment its view count (a deflection event).
    Task<KbArticleReadDto?> GetByIdAsync(string id);
    /// D7-4 — deflection suggestions for a query (does NOT increment view counts).
    Task<IEnumerable<KbSuggestionDto>> SuggestAsync(string? query, int take = 5);
    Task<KbArticleReadDto> CreateAsync(CreateKbArticleDto dto, string actor);
    Task<KbArticleReadDto> UpdateAsync(string id, UpdateKbArticleDto dto, string actor);
    Task<bool> DeleteAsync(string id);
}
