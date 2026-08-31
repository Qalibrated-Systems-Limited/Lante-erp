using ComplianceService.Core.DTOs.Surveys;

namespace ComplianceService.Core.DTOs.Dashboard;

public class QualityDashboardDto
{
    public int TotalResponses { get; set; }
    public decimal AverageRating { get; set; }
    public List<RatingBreakdownItemDto> RatingBreakdown { get; set; } = new();
    public List<FeedbackTypeBreakdownItemDto> FeedbackTypeBreakdown { get; set; } = new();
    public List<CustomerSurveyResponseReadDto> RecentResponses { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class RatingBreakdownItemDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FeedbackTypeBreakdownItemDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
