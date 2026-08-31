using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class PerDiemReturnForm : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public string? ProjectName { get; set; }

    // Summary totals (stored directly)
    public decimal TotalAmount { get; set; }   // stores TotalAdvanced
    public decimal TotalSpent { get; set; }
    public string? Notes { get; set; }
    public string? DetailsJson { get; set; }
    public string? LineItemsJson { get; set; }

    // Category breakdown (kept for compatibility)
    public decimal FaresOrCarExpense { get; set; }
    public decimal Mileage { get; set; }
    public decimal Meals { get; set; }
    public decimal Medical { get; set; }
    public decimal Incidentals { get; set; }

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

    public Assignment Assignment { get; set; } = null!;
}
