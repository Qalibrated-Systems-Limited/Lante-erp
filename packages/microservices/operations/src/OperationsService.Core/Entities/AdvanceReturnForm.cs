using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class AdvanceReturnForm : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }   // stores TotalAdvanced
    public decimal TotalAccountedFor { get; set; }
    public decimal AmountReturned { get; set; }
    public string? Notes { get; set; }
    public string? DetailsJson { get; set; }
    public string? AttachmentPath { get; set; }
    public ReturnFormStatus Status { get; set; } = ReturnFormStatus.Pending;

    // Manager review
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ManagerComments { get; set; }

    // CFO review
    public string? CfoReviewedBy { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public string? CfoComments { get; set; }

    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    public ICollection<AdvanceReturnLineItem> LineItems { get; set; } = new List<AdvanceReturnLineItem>();
    public Assignment Assignment { get; set; } = null!;
}

public class AdvanceReturnLineItem : BaseEntity
{
    public string AdvanceReturnFormId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptNumber { get; set; }

    public AdvanceReturnForm Form { get; set; } = null!;
}
