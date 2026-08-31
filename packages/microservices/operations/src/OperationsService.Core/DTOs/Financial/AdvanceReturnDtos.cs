namespace OperationsService.Core.DTOs.Financial;

public class CreateAdvanceReturnDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public decimal TotalAdvanced { get; set; }
    public string? Notes { get; set; }
    public string? Details { get; set; }
    public List<AdvanceReturnLineItemDto> LineItems { get; set; } = [];
}

public class AdvanceReturnLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptUrl { get; set; }
}

public class ReviewAdvanceReturnDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public decimal? ApprovedAmount { get; set; }
}

public class AdvanceReturnReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string SubmittedByUserId { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAdvanced { get; set; }
    public decimal TotalAccountedFor { get; set; }
    public decimal AmountReturned { get; set; }
    public string? Notes { get; set; }
    public string? DetailsJson { get; set; }
    public string? ManagerComments { get; set; }
    public string? ReviewedByManagerId { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? CfoComments { get; set; }
    public string? ReviewedByCfoId { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public List<AdvanceReturnLineItemDto> LineItems { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
