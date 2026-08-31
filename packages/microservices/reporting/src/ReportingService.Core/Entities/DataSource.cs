using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-006/007: a named, resolvable metric within one of ReportsController's reports. MetricKey
// is looked up in MetricResolverService's typed extractor dictionary (not a generic reflection
// path — the 9 report DTOs are too differently shaped for that to be robust) to get the current
// value. Widgets/scorecards/red-flag rules all bind to a DataSource rather than a raw report.
public class DataSource : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ReportCategory ModuleName { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public string? Description { get; set; }
}
