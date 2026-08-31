using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-009: one live tile on a Dashboard, sourcing its value from a DataSource via
// MetricResolverService at render time. Position is a plain sort order — a fixed grid, not a
// drag-drop layout (explicitly out of scope for this phase).
public class DashboardWidget : BaseEntity
{
    public string DashboardId { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public WidgetType WidgetType { get; set; } = WidgetType.Number;
    public int Position { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 300;
}
