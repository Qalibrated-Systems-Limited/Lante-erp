namespace OperationsService.Core.DTOs.Feedback;

public class RiskEntryDto
{
    public string  Id           { get; set; } = string.Empty;
    public string  AssignmentId { get; set; } = string.Empty;
    public string  Description  { get; set; } = string.Empty;
    public string  Likelihood   { get; set; } = string.Empty;
    public string  Impact       { get; set; } = string.Empty;
    public string? Mitigation   { get; set; }
    public string? Owner        { get; set; }
    public string  Status       { get; set; } = string.Empty;
    public DateTime CreatedAt   { get; set; }
}

public class CreateRiskDto
{
    public string  Description  { get; set; } = string.Empty;
    public string  Likelihood   { get; set; } = "Low";
    public string  Impact       { get; set; } = "Low";
    public string? Mitigation   { get; set; }
    public string? Owner        { get; set; }
}

public class UpdateRiskDto
{
    public string? Mitigation   { get; set; }
    public string? Status       { get; set; }
    public string? Owner        { get; set; }
}

public class CustomerFeedbackDto
{
    public string  Id                    { get; set; } = string.Empty;
    public string  AssignmentId          { get; set; } = string.Empty;
    public int     OverallRating         { get; set; }
    public int?    TimelinessRating      { get; set; }
    public int?    QualityRating         { get; set; }
    public int?    ProfessionalismRating { get; set; }
    public bool?   WouldRecommend        { get; set; }
    public string? Comments              { get; set; }
    public string? CapturedByName        { get; set; }
    public DateTime CreatedAt            { get; set; }
}

public class CreateFeedbackDto
{
    public int     OverallRating         { get; set; }
    public int?    TimelinessRating      { get; set; }
    public int?    QualityRating         { get; set; }
    public int?    ProfessionalismRating { get; set; }
    public bool?   WouldRecommend        { get; set; }
    public string? Comments              { get; set; }
}
