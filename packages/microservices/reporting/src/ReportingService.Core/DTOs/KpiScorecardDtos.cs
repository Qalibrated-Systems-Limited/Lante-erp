using ReportingService.Core.Enums;

namespace ReportingService.Core.DTOs;

public class KpiScorecardReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string? DataSourceName { get; set; }
    public decimal TargetValue { get; set; }
    public decimal WarningThreshold { get; set; }
    public decimal CriticalThreshold { get; set; }
    public ScorecardPeriod Period { get; set; }
}

public class CreateKpiScorecardDto
{
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal WarningThreshold { get; set; }
    public decimal CriticalThreshold { get; set; }
    public ScorecardPeriod Period { get; set; }
}

public class KpiScorecardStatusDto
{
    public string ScorecardId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal TargetValue { get; set; }
    public decimal Variance { get; set; }
    public ScorecardStatus Status { get; set; }
    public string? Error { get; set; }
}
