namespace OperationsService.Core.DTOs.Claims;

public class CreateClaimDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Justification { get; set; }
    public string? ReceiptUrl { get; set; }
}

public class UpdateClaimDto
{
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? Justification { get; set; }
    public string? ReceiptUrl { get; set; }
}

public class ReviewClaimDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public decimal? ApprovedAmount { get; set; }
}

public class ClaimReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string ClaimantUserId { get; set; } = string.Empty;
    public string ClaimantName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Justification { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? ManagerComments { get; set; }
    public string? CfoComments { get; set; }
    public string? ReviewedByManagerId { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? ReviewedByCfoId { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClaimFilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string? AssignmentId { get; set; }
    public string? ClaimantUserId { get; set; }
    public int? Status { get; set; }
    public bool SortDescending { get; set; } = true;
}
