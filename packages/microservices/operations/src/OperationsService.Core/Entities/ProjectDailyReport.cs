namespace OperationsService.Core.Entities;

// Project-level daily site report (distinct from the assignment-scoped DailySummary).
// Logged by the site team each day: work done, issues, and plan for tomorrow.
public class ProjectDailyReport : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }
    public string WorkDone { get; set; } = string.Empty;
    public string? Issues { get; set; }
    public string? PlannedForTomorrow { get; set; }
    public bool IsReviewed { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string SubmittedByUserId { get; set; } = string.Empty;

    public Project Project { get; set; } = null!;
}
