using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P12 (CRM-057) — CLIENT_COMPLAINT. Logged, assigned to an owner, worked and resolved;
/// feeds the monthly MD complaints report. Distinct from a helpdesk ticket — this is the CRM/account
/// view of client dissatisfaction against the account owner's book.</summary>
public class ClientComplaint : BaseEntity
{
    public string ComplaintNumber { get; set; } = string.Empty;  // CMP-{yr}-{seq}
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public ComplaintSeverity Severity { get; set; } = ComplaintSeverity.Medium;
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string? Resolution { get; set; }
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
