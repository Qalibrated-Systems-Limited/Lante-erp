using ReportingService.Core.Enums;

namespace ReportingService.Core.DTOs;

public class RedFlagRuleReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string? DataSourceName { get; set; }
    public RedFlagCondition Condition { get; set; }
    public decimal ThresholdValue { get; set; }
    public RedFlagSeverity Severity { get; set; }
    public bool IsActive { get; set; }
}

public class CreateRedFlagRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public RedFlagCondition Condition { get; set; }
    public decimal ThresholdValue { get; set; }
    public RedFlagSeverity Severity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RedFlagEventReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RedFlagRuleId { get; set; } = string.Empty;
    public string? RuleName { get; set; }
    public DateTime TriggeredAt { get; set; }
    public RedFlagSeverity Severity { get; set; }
    public decimal Value { get; set; }
    public int DaysOpen { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public RedFlagEventStatus Status { get; set; }
}
