namespace OperationsService.Core.DTOs.Financial;

public class CreateRefundDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReceiptUrl { get; set; }
}

public class ReviewRefundDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
}

public class RefundReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReceiptUrl { get; set; }
    public string? ReviewComments { get; set; }
    public string? ReviewedByManagerId { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
