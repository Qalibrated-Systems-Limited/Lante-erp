using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class ProjectApproval : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public ApprovalType ApprovalType { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }

    public Project Project { get; set; } = null!;
}
