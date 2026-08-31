using HSEService.Core.DTOs.Common;
using HSEService.Core.Enums;

namespace HSEService.Core.DTOs.Incidents;

public class CreateHseIncidentDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Description { get; set; } = string.Empty;

    // HSE-007: flag as an environmental incident to spawn the EnvIncident 1:1 extension.
    public bool IsEnvironmental { get; set; }
    public string? NemaRef { get; set; }

    // Optional first corrective action captured at report time (CAPA required within 48h).
    public string? CorrectiveActionDescription { get; set; }
    public string? CorrectiveActionOwnerUserId { get; set; }
    public string? CorrectiveActionOwnerName { get; set; }
    public DateTime? CorrectiveActionDueDate { get; set; }
}

public class UpdateHseIncidentStatusDto
{
    public IncidentStatus Status { get; set; }
}

// Core-field edit — Status stays workflow-controlled (see UpdateHseIncidentStatusDto), and the
// EnvIncident extension / CorrectiveActions are managed by their own dedicated flows.
public class UpdateHseIncidentDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class EnvIncidentDto
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string? NemaRef { get; set; }
    public bool NemaNotificationRequired { get; set; }
    public DateTime? NotifiedAt { get; set; }
}

public class CorrectiveActionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public CorrectiveActionStatus Status { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class HseIncidentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ReportedByUserId { get; set; } = string.Empty;
    public string? ReportedByName { get; set; }
    public string Description { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public EnvIncidentDto? EnvIncident { get; set; }
    public List<CorrectiveActionReadDto> CorrectiveActions { get; set; } = new();
}

public class CreateCorrectiveActionDto
{
    public string IncidentId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime DueDate { get; set; }
}

public class UpdateCorrectiveActionDto
{
    public string Description { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public CorrectiveActionStatus Status { get; set; }
    public DateTime DueDate { get; set; }
}

public class CorrectiveActionFilterParameters : PaginationParameters
{
    public string? IncidentId { get; set; }
    public bool OpenOnly { get; set; } = false;
}
