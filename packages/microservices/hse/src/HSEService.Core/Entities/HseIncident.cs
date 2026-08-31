using HSEService.Core.Enums;

namespace HSEService.Core.Entities;

// HSE-001: near-miss / first aid / medical treatment / lost-time injury capture.
// Central event of the module — CorrectiveAction(s) and an optional EnvIncident hang off it.
public class HseIncident : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ReportedByUserId { get; set; } = string.Empty;
    public string? ReportedByName { get; set; }
    public string Description { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;

    public EnvIncident? EnvIncident { get; set; }
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
}
