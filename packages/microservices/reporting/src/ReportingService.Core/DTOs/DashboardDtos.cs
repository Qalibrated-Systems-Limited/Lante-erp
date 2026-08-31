using ReportingService.Core.Enums;

namespace ReportingService.Core.DTOs;

public class DashboardReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public string? OwnerUserId { get; set; }
}

public class CreateDashboardDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public string? OwnerUserId { get; set; }
}

public class DashboardWidgetReadDto
{
    public string Id { get; set; } = string.Empty;
    public string DashboardId { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string? DataSourceName { get; set; }
    public string Title { get; set; } = string.Empty;
    public WidgetType WidgetType { get; set; }
    public int Position { get; set; }
    public int RefreshIntervalSeconds { get; set; }
}

public class CreateDashboardWidgetDto
{
    public string DataSourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public WidgetType WidgetType { get; set; } = WidgetType.Number;
    public int Position { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 300;
}

public class DashboardWidgetValueDto
{
    public string WidgetId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public WidgetType WidgetType { get; set; }
    public int Position { get; set; }
    public decimal Value { get; set; }
    public string? Error { get; set; }
}

public class DashboardDataDto
{
    public string DashboardId { get; set; } = string.Empty;
    public List<DashboardWidgetValueDto> Widgets { get; set; } = new();
}
