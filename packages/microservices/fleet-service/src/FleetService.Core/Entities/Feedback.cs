namespace FleetService.Core.Entities;

public class Feedback : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public FleetFeedbackType FeedbackType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FleetFeedbackStatus Status { get; set; } = FleetFeedbackStatus.Pending;
    public string? AdminResponse { get; set; }
    public string? RespondedByUserId { get; set; }
    public DateTime? ResponseDate { get; set; }
}

public enum FleetFeedbackType { BugReport, FeatureRequest, General, Complaint, Suggestion }
public enum FleetFeedbackStatus { Pending, Reviewed, InProgress, Resolved, Rejected }
