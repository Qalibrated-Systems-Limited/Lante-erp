using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class Ticket : BaseEntity
{
    /// D1-4 — per-tenant sequential ticket number (1, 2, 3, …). Assigned on creation; the human-facing
    /// reference is the zero-padded form (see <see cref="Reference"/>). 0 for legacy rows created
    /// before this field existed — those fall back to the old GUID-prefix reference.
    public int TicketNumber { get; set; }

    /// D1-4 — human-facing reference. Sequential (TKT-000123) once numbered; falls back to the legacy
    /// GUID-prefix for pre-D1-4 rows so already-issued references keep resolving. Not persisted.
    public string Reference => TicketNumber > 0
        ? $"TKT-{TicketNumber:D6}"
        : Id[..8].ToUpperInvariant();

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketSource Source { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    /// D1-2 — the helpdesk client this ticket belongs to (local Customer list; CRM-swappable). Nullable
    /// so internal/IT tickets and legacy rows without a client still work.
    public string? CustomerId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public string? AssigneeName { get; set; }
    public string? SecondaryTeamId { get; set; }
    public LinkedEntityType LinkedEntityType { get; set; } = LinkedEntityType.None;
    public string? LinkedEntityId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// #3 — first public reply by staff (not the requester). Response SLA is met when this is set,
    /// independent of status (auto-assignment used to leave New instantly, so status was a bad proxy).
    public DateTime? FirstResponseAt { get; set; }
    /// #4 — SLA/escalation clock pause. Set while the ticket sits in Pending (waiting on the customer);
    /// on resume the paused span is added back to the deadlines. SlaPausedHours accumulates total
    /// paused time so escalation measures active hours, not wall-clock since creation.
    public DateTime? SlaPausedAt { get; set; }
    public double SlaPausedHours { get; set; }
    /// #7 — set when the post-resolution satisfaction survey has been sent, so the follow-up worker
    /// doesn't re-send it on every 5-minute pass between the 24h mark and auto-close.
    public DateTime? SurveySentAt { get; set; }
    /// #1 — the external requester's email (portal submissions) so the follow-up worker can email a
    /// warning before auto-closing a ticket waiting on the customer. Null for internal-only tickets.
    public string? RequesterEmail { get; set; }
    /// #19 — set when this ticket was merged into another (a duplicate); points at the surviving ticket.
    public string? MergedIntoTicketId { get; set; }
    /// #1 — set when the pre-auto-close warning has been emailed, so it's sent once per Pending stint
    /// (cleared by the transition gate when the ticket leaves Pending).
    public DateTime? PendingWarningSentAt { get; set; }
    /// D4-1 — set when the auto-acknowledgement (ticket#/category/SLA) has been sent to the requester.
    public DateTime? AckSentAt { get; set; }
    /// D4-3 — flagged when the same client raised another ticket in the same category within 30 days.
    public bool IsRepeat { get; set; }
    public string? ResolutionNotes { get; set; }
    /// D3-3 — root-cause analysis, distinct from ResolutionNotes (what was done). Mandatory on a
    /// manual resolve, so a ticket can't reach Closed without it.
    public string? RootCause { get; set; }
    /// D3-1 — parent ticket for parent/child linking (distinct from MergedIntoTicketId dedup). A child
    /// inherits status-close when the parent resolves/closes. Null for standalone/parent tickets.
    public string? ParentTicketId { get; set; }
    /// D3-2 — context references into Module 5 (Projects) and field service reports. Read-only links
    /// surfaced on the ticket; may be stubbed until those modules expose lookups.
    public string? ProjectId { get; set; }
    public string? FsrId { get; set; }
    /// D7-1 — internal IT-helpdesk fields (set on IT-category tickets): the reporting employee (HR
    /// Module 3), their branch, and the affected system. Null for non-IT tickets.
    public string? EmployeeId { get; set; }
    public string? BranchId { get; set; }
    public string? SystemAffected { get; set; }
    /// #5 — why the ticket was closed (done vs cancelled vs auto-closed) + free-text reason
    /// (e.g. the field cancellation reason). Null until the ticket reaches Closed.
    public ClosureType? ClosureType { get; set; }
    public string? ClosureReason { get; set; }
    public bool IsEscalated { get; set; } = false;
    public EscalationLevel? EscalationLevel { get; set; }
    public bool RequiresEvidence { get; set; } = false;
    public string? OperationsAssignmentId { get; set; }

    // Navigations
    public TicketCategory Category { get; set; } = null!;
    public Customer? Customer { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
    public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
    public ICollection<TicketEscalation> Escalations { get; set; } = new List<TicketEscalation>();
    public ICollection<TicketWatcher> Watchers { get; set; } = new List<TicketWatcher>();
    public ICollection<TicketTag> Tags { get; set; } = new List<TicketTag>();
    public TicketSatisfactionRating? SatisfactionRating { get; set; }
}
