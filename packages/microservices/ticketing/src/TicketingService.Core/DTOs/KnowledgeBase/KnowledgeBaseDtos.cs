namespace TicketingService.Core.DTOs.KnowledgeBase;

public class KbArticleReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string ResolutionSteps { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public int ViewCount { get; set; }
    public string? LinkedTicketIds { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// D7-4 — lightweight shape for the deflection suggestion list (no view-count increment).
public class KbSuggestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
}

public class CreateKbArticleDto
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string ResolutionSteps { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public string? LinkedTicketIds { get; set; }
    public bool IsPublished { get; set; } = true;
}

public class UpdateKbArticleDto
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Problem { get; set; }
    public string? ResolutionSteps { get; set; }
    public string? Keywords { get; set; }
    public string? LinkedTicketIds { get; set; }
    public bool? IsPublished { get; set; }
}
