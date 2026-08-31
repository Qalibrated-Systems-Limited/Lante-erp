namespace OperationsService.Core.DTOs.Financial;

public class CreatePerDiemReturnDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public decimal TotalAdvanced { get; set; }
    public decimal TotalSpent { get; set; }
    public string? Notes { get; set; }
    public string? Details { get; set; }
    public List<PerDiemLineItemDto> LineItems { get; set; } = [];
}

public class PerDiemLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptUrl { get; set; }
}

public class ReviewPerDiemReturnDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public decimal? ApprovedAmount { get; set; }
}

public class PerDiemReturnReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAdvanced { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal Balance { get; set; }
    public string? Notes { get; set; }
    public string? DetailsJson { get; set; }
    public string? ManagerComments { get; set; }
    public string? ReviewedByManagerId { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? CfoComments { get; set; }
    public string? ReviewedByCfoId { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public List<PerDiemLineItemDto> LineItems { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
