namespace TicketingService.Core.Entities;

/// D7-3 — a knowledge-base article (HELP-011). Surfaced as a deflection suggestion before a ticket is
/// submitted; ViewCount tracks how often it was opened (the deflection metric). LinkedTicketIds ties
/// articles back to the tickets they resolved.
public class KnowledgeBaseArticle : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string ResolutionSteps { get; set; } = string.Empty;
    /// Comma-separated search keywords.
    public string? Keywords { get; set; }
    public int ViewCount { get; set; }
    /// Comma-separated ticket ids this article has resolved / is linked to.
    public string? LinkedTicketIds { get; set; }
    public bool IsPublished { get; set; } = true;
}
