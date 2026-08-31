namespace TicketingService.Core.DTOs.Satisfaction;

public class SubmitRatingDto
{
    public int Rating { get; set; } // 1–5
    public string? Comment { get; set; }
}

public class SatisfactionRatingReadDto
{
    public string Id { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string SubmittedByUserId { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

// #14 — aggregate customer-satisfaction report.
public class CsatSummaryDto
{
    public int TotalResponses { get; set; }
    public double AverageRating { get; set; }
    public double CsatPercent { get; set; }                 // % of responses rated 4–5
    public Dictionary<int, int> Distribution { get; set; } = new();  // star (1..5) → count
    public List<CsatCommentDto> RecentComments { get; set; } = new();
    // D6-1 — survey response rate: responses / surveys sent.
    public int SurveysSent { get; set; }
    public double ResponseRatePct { get; set; }
}

public class CsatCommentDto
{
    public string TicketId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; }
}

// D6-3 — the customer-service dashboard: the spec's five panels in one aggregate.
public class CsDashboardDto
{
    public int OpenTotal { get; set; }
    public List<CategoryOpenDto> OpenByCategory { get; set; } = new();   // open tickets by category + aging
    public double SlaCompliancePct { get; set; }
    public double AvgResolutionHours { get; set; }
    public double AvgSatisfaction { get; set; }
    public int SurveysSent { get; set; }
    public int SurveyResponses { get; set; }
    public double ResponseRatePct { get; set; }
}

public class CategoryOpenDto
{
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Open { get; set; }
    public int AgingOver72h { get; set; }   // open longer than 72 active hours — needs attention
}
