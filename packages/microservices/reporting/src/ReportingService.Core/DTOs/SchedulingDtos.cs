using ReportingService.Core.Enums;

namespace ReportingService.Core.DTOs;

public class ReportDefinitionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReportCategory Category { get; set; }
    public bool IsActive { get; set; }
}

public class ReportScheduleReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ReportDefinitionId { get; set; } = string.Empty;
    public string? ReportDefinitionName { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
}

public class CreateReportScheduleDto
{
    public string ReportDefinitionId { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public ReportFormat Format { get; set; } = ReportFormat.Excel;
}

public class UpdateReportScheduleDto
{
    public string CronExpression { get; set; } = string.Empty;
    public ReportFormat Format { get; set; } = ReportFormat.Excel;
    public bool IsActive { get; set; } = true;
}

public class ReportRecipientReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ReportScheduleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateReportRecipientDto
{
    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; }
}

public class ReportRunReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ReportDefinitionId { get; set; } = string.Empty;
    public string? ReportDefinitionName { get; set; }
    public string? ReportScheduleId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public ReportFormat Format { get; set; }
    public string? FileUrl { get; set; }
    public RunStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
}
