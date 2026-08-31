using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Satisfaction;
using TicketingService.Core.DTOs.Tags;
using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Tickets;

public class TicketReadDto
{
    public string Id { get; set; } = string.Empty;
    /// D1-4 — sequential number + its human-facing reference (TKT-000123).
    public int TicketNumber { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public string PriorityLabel => Priority.ToString();
    public TicketStatus Status { get; set; }
    public string StatusLabel => Status.ToString();
    public TicketSource Source { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCompany { get; set; }
    public string? ClientReference { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public string? AssigneeName { get; set; }
    public string? SecondaryTeamId { get; set; }
    public LinkedEntityType LinkedEntityType { get; set; }
    public string? LinkedEntityId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? SlaPausedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? RootCause { get; set; }
    public string? ParentTicketId { get; set; }
    public string? ProjectId { get; set; }
    public string? FsrId { get; set; }
    public string? EmployeeId { get; set; }
    public string? BranchId { get; set; }
    public string? SystemAffected { get; set; }
    public ClosureType? ClosureType { get; set; }
    public string? ClosureTypeLabel => ClosureType?.ToString();
    public string? ClosureReason { get; set; }
    public string? MergedIntoTicketId { get; set; }
    public DateTime? AckSentAt { get; set; }
    public bool IsRepeat { get; set; }
    public bool IsEscalated { get; set; }
    public EscalationLevel? EscalationLevel { get; set; }
    public bool RequiresEvidence { get; set; }
    public string? OperationsAssignmentId { get; set; }
    public bool IsResponseBreached { get; set; }
    public bool IsResolutionBreached { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CommentReadDto> Comments { get; set; } = new();
    public List<TicketHistoryReadDto> History { get; set; } = new();
    public List<TicketAttachmentReadDto> Attachments { get; set; } = new();
    public List<TagReadDto> Tags { get; set; } = new();
    public SatisfactionRatingReadDto? SatisfactionRating { get; set; }
}

public class TicketHistoryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// D2-5 — one entry in a ticket's ownership trail (who was assigned, by whom, when, handover notes).
public class TicketAssignmentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignedToUserId { get; set; } = string.Empty;
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string? HandoverNotes { get; set; }
    public bool IsPrimary { get; set; }
}

public class TicketAttachmentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
