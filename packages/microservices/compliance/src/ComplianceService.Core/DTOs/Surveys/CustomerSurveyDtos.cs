namespace ComplianceService.Core.DTOs.Surveys;

// FeedbackType / Rating travel as strings for easy JSON binding from the public form —
// parsed server-side with Enum.TryParse (see PublicCustomerSurveyController.Submit).
public class CreateCustomerSurveyResponseDto
{
    public string RespondentName { get; set; } = string.Empty;
    public string CorporationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CounsellorName { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
}

public class CustomerSurveyResponseReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RespondentName { get; set; } = string.Empty;
    public string CorporationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CounsellorName { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
