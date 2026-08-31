namespace OperationsService.Core.Entities;

public class CustomerFeedback : BaseEntity
{
    public string AssignmentId           { get; set; } = string.Empty;
    public int    OverallRating          { get; set; }   // 1–5
    public int?   TimelinessRating       { get; set; }
    public int?   QualityRating          { get; set; }
    public int?   ProfessionalismRating  { get; set; }
    public bool?  WouldRecommend         { get; set; }
    public string? Comments              { get; set; }
    public string? CapturedById          { get; set; }
    public string? CapturedByName        { get; set; }

    public Assignment Assignment         { get; set; } = null!;
}
