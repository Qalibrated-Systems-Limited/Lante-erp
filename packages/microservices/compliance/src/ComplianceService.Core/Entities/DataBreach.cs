using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-005: 72-hour ODPC notification timer (Data Protection Act 2019) computed at
// creation from DiscoveredAt, plus remediation tracking.
public class DataBreach : BaseEntity
{
    public DateTime OccurredAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OdpcNotificationDueAt { get; set; }
    public DateTime? OdpcNotifiedAt { get; set; }
    public DataBreachStatus Status { get; set; } = DataBreachStatus.Open;
    public string? RemediationNotes { get; set; }
}
