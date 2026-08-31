using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class PettyCashAdvanceForm : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public decimal Sum { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public PettyCashStatus Status { get; set; } = PettyCashStatus.Pending;

    // Manager review
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalComments { get; set; }

    // CFO review
    public string? CfoReviewedBy { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public string? CfoComments { get; set; }

    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    public string? DisbursedBy { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public string? VoucherNumber { get; set; }
    public string? ReferenceNumber { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
