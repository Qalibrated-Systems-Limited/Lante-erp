using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Breaches;

public class DataBreachReadDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OdpcNotificationDueAt { get; set; }
    public DateTime? OdpcNotifiedAt { get; set; }
    public DataBreachStatus Status { get; set; }
    public string? RemediationNotes { get; set; }
}

public class CreateDataBreachDto
{
    public DateTime OccurredAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class UpdateDataBreachDto
{
    public DataBreachStatus Status { get; set; }
    public DateTime? OdpcNotifiedAt { get; set; }
    public string? RemediationNotes { get; set; }
}

// Core-field edit — Status/OdpcNotifiedAt/RemediationNotes stay workflow-controlled (see
// UpdateDataBreachDto). OdpcNotificationDueAt is recomputed from DiscoveredAt, same 72-hour rule.
public class UpdateDataBreachDetailsDto
{
    public DateTime OccurredAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public string Description { get; set; } = string.Empty;
}
