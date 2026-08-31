using ComplianceService.Core.DTOs.Dashboard;
using ComplianceService.Core.DTOs.Surveys;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Core.Services;

public class QualityDashboardService(
    IComplianceCrudService<CustomerSurveyResponse> surveyResponses) : IQualityDashboardService
{
    private static readonly Dictionary<ServiceRating, string> RatingLabels = new()
    {
        [ServiceRating.Outstanding] = "Outstanding",
        [ServiceRating.Good] = "Good",
        [ServiceRating.Average] = "Average",
        [ServiceRating.Poor] = "Poor",
        [ServiceRating.VeryPoor] = "Very Poor",
    };

    private static readonly Dictionary<SurveyFeedbackType, string> FeedbackTypeLabels = new()
    {
        [SurveyFeedbackType.Compliment] = "Compliment",
        [SurveyFeedbackType.Complaint] = "Complaint",
        [SurveyFeedbackType.GeneralFeedback] = "General Feedback",
    };

    public async Task<QualityDashboardDto> ComputeAsync()
    {
        var all = await surveyResponses.GetAllAsync();
        var responses = all.ToList();

        var ratingBreakdown = Enum.GetValues<ServiceRating>()
            .OrderByDescending(r => (int)r)
            .Select(r => new RatingBreakdownItemDto
            {
                Label = RatingLabels[r],
                Count = responses.Count(x => x.Rating == r),
            })
            .ToList();

        var feedbackTypeBreakdown = Enum.GetValues<SurveyFeedbackType>()
            .Select(t => new FeedbackTypeBreakdownItemDto
            {
                Label = FeedbackTypeLabels[t],
                Count = responses.Count(x => x.FeedbackType == t),
            })
            .ToList();

        var recent = responses
            .OrderByDescending(r => r.SubmittedAt)
            .Take(20)
            .Select(ToReadDto)
            .ToList();

        return new QualityDashboardDto
        {
            TotalResponses = responses.Count,
            AverageRating = responses.Count == 0 ? 0m : Math.Round((decimal)responses.Average(r => (int)r.Rating), 2),
            RatingBreakdown = ratingBreakdown,
            FeedbackTypeBreakdown = feedbackTypeBreakdown,
            RecentResponses = recent,
        };
    }

    private static CustomerSurveyResponseReadDto ToReadDto(CustomerSurveyResponse r) => new()
    {
        Id = r.Id,
        RespondentName = r.RespondentName,
        CorporationName = r.CorporationName,
        Email = r.Email,
        CounsellorName = r.CounsellorName,
        FeedbackType = FeedbackTypeLabels[r.FeedbackType],
        Details = r.Details,
        Rating = RatingLabels[r.Rating],
        SubmittedAt = r.SubmittedAt,
    };
}
