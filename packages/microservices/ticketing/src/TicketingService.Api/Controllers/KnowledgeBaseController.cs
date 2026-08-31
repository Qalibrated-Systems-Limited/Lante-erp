using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.KnowledgeBase;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// D7-3/D7-4 — knowledge-base articles + deflection search.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/kb")]
public class KnowledgeBaseController(IKnowledgeBaseService kb) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<KbArticleReadDto>>>> GetAll()
        => Ok(ApiResponse<IEnumerable<KbArticleReadDto>>.Ok(await kb.GetAllAsync()));

    // D7-4 — deflection suggestions (no view-count increment).
    [HttpGet("suggest")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<KbSuggestionDto>>>> Suggest([FromQuery] string? q)
        => Ok(ApiResponse<IEnumerable<KbSuggestionDto>>.Ok(await kb.SuggestAsync(q)));

    // Opening an article increments its view count (the deflection metric).
    [HttpGet("{id}")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<KbArticleReadDto>>> GetById(string id)
    {
        var article = await kb.GetByIdAsync(id);
        return article == null
            ? NotFound(ApiResponse<KbArticleReadDto>.Fail("Article not found.", 404))
            : Ok(ApiResponse<KbArticleReadDto>.Ok(article));
    }

    [HttpPost]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<KbArticleReadDto>>> Create([FromBody] CreateKbArticleDto dto)
        => Ok(ApiResponse<KbArticleReadDto>.Ok(await kb.CreateAsync(dto, CurrentUserId), "Article created."));

    [HttpPut("{id}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<KbArticleReadDto>>> Update(string id, [FromBody] UpdateKbArticleDto dto)
        => Ok(ApiResponse<KbArticleReadDto>.Ok(await kb.UpdateAsync(id, dto, CurrentUserId), "Article updated."));

    [HttpDelete("{id}")]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(string id)
    {
        var ok = await kb.DeleteAsync(id);
        return ok
            ? Ok(ApiResponse<bool>.Ok(true, "Article deleted."))
            : NotFound(ApiResponse<bool>.Fail("Article not found.", 404));
    }
}
