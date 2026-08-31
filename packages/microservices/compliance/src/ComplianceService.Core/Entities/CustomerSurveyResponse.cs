using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// Public, anonymous customer-satisfaction survey (Quality dashboard's data source). No login,
// no OTP — pure feedback capture, nothing gets actioned downstream (unlike the ticketing portal's
// OTP-verified service-request flow). Submitted via PublicCustomerSurveyController and aggregated
// by QualityDashboardService.
public class CustomerSurveyResponse : BaseEntity
{
    public string RespondentName { get; set; } = string.Empty;
    public string CorporationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CounsellorName { get; set; } = string.Empty;
    public SurveyFeedbackType FeedbackType { get; set; }
    public string Details { get; set; } = string.Empty;
    public ServiceRating Rating { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
