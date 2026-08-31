namespace OperationsService.Core.DTOs.Projects;

public class CreateProjectDailyReportDto
{
    public DateTime ReportDate { get; set; }
    public string WorkDone { get; set; } = string.Empty;
    public string? Issues { get; set; }
    public string? PlannedForTomorrow { get; set; }
}

public class ProjectDailyReportReadDto
{
    public string  Id { get; set; } = string.Empty;
    public string  ProjectId { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; }
    public string  WorkDone { get; set; } = string.Empty;
    public string? Issues { get; set; }
    public string? PlannedForTomorrow { get; set; }
    public bool    IsReviewed { get; set; }
    public string  SubmittedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
