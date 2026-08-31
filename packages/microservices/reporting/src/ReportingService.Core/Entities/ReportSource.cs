namespace ReportingService.Core.Entities;

// RPT-007: M:N join — which DataSources feed a given ReportDefinition. One row per (report,
// source) pair; today that's 1:1 (one headline metric seeded per report) but the join supports
// a report growing multiple tracked metrics later without a schema change.
public class ReportSource : BaseEntity
{
    public string ReportDefinitionId { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
}
