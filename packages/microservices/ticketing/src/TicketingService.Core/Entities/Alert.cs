namespace TicketingService.Core.Entities;

// A persisted "needs attention" record, distinct from Notification: notifications are a
// personal inbox item for one user, alerts are a tenant-wide list of open issues (SLA
// breaches, escalations, and eventually license/asset expiry etc.) that anyone with access
// can see and acknowledge, regardless of who — if anyone — was individually notified.
public class Alert : BaseEntity
{
    public string Source   { get; set; } = string.Empty; // "SLA", "Escalation", ...
    public string Severity { get; set; } = "Warning";    // Info | Warning | Critical
    public string Title    { get; set; } = string.Empty;
    public string Message  { get; set; } = string.Empty;

    public string? TicketId { get; set; }
    public string? TicketTitle { get; set; }
    public string? AssignedToUserId { get; set; } // who was notified about it, if anyone

    // Domain-specific gate on top of AssignedToUserId: lets a source (HSE, Compliance, ...) mark
    // an unassigned alert as visible only to holders of a given permission (e.g. "hse.read",
    // "compliance.approve") instead of falling back to the blanket alerts.read.all/system.admin
    // gate everyone-or-nobody-unassigned used previously. Null preserves the old behaviour exactly
    // for alerts that predate this field (SLA/Escalation) or genuinely have no natural owner tier.
    public string? RequiredPermission { get; set; }

    // Seen = someone with access opened/viewed it, but it isn't resolved yet.
    // Acknowledged = actually handled/resolved. Distinct so admins can tell "nobody's even
    // looked at this" apart from "someone saw it and is presumably working it."
    public bool     IsSeen   { get; set; }
    public string?  SeenBy   { get; set; }
    public DateTime? SeenAt  { get; set; }

    public bool     IsAcknowledged { get; set; }
    public string?  AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public Ticket? Ticket { get; set; }
}
