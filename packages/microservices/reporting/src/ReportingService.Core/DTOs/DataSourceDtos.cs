using ReportingService.Core.Enums;

namespace ReportingService.Core.DTOs;

public class DataSourceReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReportCategory ModuleName { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DataSourceValueDto
{
    public string DataSourceId { get; set; } = string.Empty;
    public string MetricKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime ResolvedAt { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}
