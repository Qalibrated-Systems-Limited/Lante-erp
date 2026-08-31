using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Refund : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? AttachmentPath { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    public string? ManagerReviewedBy { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? ManagerComments { get; set; }

    public string? CfoReviewedBy { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public string? CfoComments { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }

    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
