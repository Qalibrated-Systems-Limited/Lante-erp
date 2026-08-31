using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-002/003: a recurring delivery of one ReportDefinition. NextRunAt is advanced by
// ReportSchedulerBackgroundService each time it fires, computed from CronExpression.
public class ReportSchedule : BaseEntity
{
    public string ReportDefinitionId { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public ReportFormat Format { get; set; } = ReportFormat.Excel;
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
}
