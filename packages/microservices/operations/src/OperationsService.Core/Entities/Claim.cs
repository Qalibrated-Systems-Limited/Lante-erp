using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Claim : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Justification { get; set; }
    public string? SupportingDocuments { get; set; }
    public string? AttachmentPath { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;
    public PaymentMethod? PaymentMethod { get; set; }

    public string? ManagerReviewedBy { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? ManagerComments { get; set; }

    public string? CfoReviewedBy { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public string? CfoComments { get; set; }
    public string? VoucherNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public string? DisbursedBy { get; set; }

    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
