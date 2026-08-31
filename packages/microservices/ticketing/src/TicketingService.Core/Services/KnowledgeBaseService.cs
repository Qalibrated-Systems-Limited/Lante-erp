using AutoMapper;
using TicketingService.Core.DTOs.KnowledgeBase;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

/// <inheritdoc cref="IKnowledgeBaseService"/>
public class KnowledgeBaseService(IKnowledgeBaseRepository repository, IMapper mapper) : IKnowledgeBaseService
{
    public async Task<IEnumerable<KbArticleReadDto>> GetAllAsync()
    {
        var articles = await repository.GetAllAsync();
        return articles
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => mapper.Map<KbArticleReadDto>(a));
    }

    public async Task<KbArticleReadDto?> GetByIdAsync(string id)
    {
        var article = await repository.GetByIdAsync(id);
        if (article == null) return null;

        // D7-4 — opening an article is a deflection event; count it.
        article.ViewCount += 1;
        await repository.UpdateAsync(article);

        return mapper.Map<KbArticleReadDto>(article);
    }

    public async Task<IEnumerable<KbSuggestionDto>> SuggestAsync(string? query, int take = 5)
    {
        var matches = await repository.SearchAsync(query, take);
        return matches.Select(a => new KbSuggestionDto
        {
            Id = a.Id,
            Title = a.Title,
            Category = a.Category,
            Problem = a.Problem,
        });
    }

    public async Task<KbArticleReadDto> CreateAsync(CreateKbArticleDto dto, string actor)
    {
        var article = new KnowledgeBaseArticle
        {
            Title           = dto.Title,
            Category        = dto.Category,
            Problem         = dto.Problem,
            ResolutionSteps = dto.ResolutionSteps,
            Keywords        = dto.Keywords,
            LinkedTicketIds = dto.LinkedTicketIds,
            IsPublished     = dto.IsPublished,
            CreatedBy       = actor,
            UpdatedBy       = actor,
        };
        var created = await repository.CreateAsync(article);
        return mapper.Map<KbArticleReadDto>(created);
    }

    public async Task<KbArticleReadDto> UpdateAsync(string id, UpdateKbArticleDto dto, string actor)
    {
        var article = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Article {id} not found.");

        if (dto.Title != null) article.Title = dto.Title;
        if (dto.Category != null) article.Category = dto.Category;
        if (dto.Problem != null) article.Problem = dto.Problem;
        if (dto.ResolutionSteps != null) article.ResolutionSteps = dto.ResolutionSteps;
        if (dto.Keywords != null) article.Keywords = dto.Keywords;
        if (dto.LinkedTicketIds != null) article.LinkedTicketIds = dto.LinkedTicketIds;
        if (dto.IsPublished.HasValue) article.IsPublished = dto.IsPublished.Value;
        article.UpdatedBy = actor;

        var updated = await repository.UpdateAsync(article);
        return mapper.Map<KbArticleReadDto>(updated);
    }

    public Task<bool> DeleteAsync(string id) => repository.DeleteAsync(id);
}
