using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Requisition : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public RequisitionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? ItemsList { get; set; }
    public string? LineItemsJson { get; set; }
    public string? Justification { get; set; }
    public RequisitionStatus Status { get; set; } = RequisitionStatus.Pending;

    public string? TmReviewedBy { get; set; }
    public DateTime? TmReviewedAt { get; set; }
    public string? TmComments { get; set; }

    public string? CfoReviewedBy { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public string? CfoComments { get; set; }
    public string? VoucherNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedBy { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
