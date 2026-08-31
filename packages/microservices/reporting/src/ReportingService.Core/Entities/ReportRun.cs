using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-005: one generated instance of a report — either scheduled (ReportScheduleId set) or
// ad-hoc/manual (null). FileUrl points at the rendered export (see ReportHelpers' export logic).
public class ReportRun : BaseEntity
{
    public string ReportDefinitionId { get; set; } = string.Empty;
    public string? ReportScheduleId { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ReportFormat Format { get; set; } = ReportFormat.Excel;
    public string? FileUrl { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string TriggeredBy { get; set; } = "scheduler";
}
