namespace OperationsService.Core.DTOs.Financial;

public class CreatePettyCashAdvanceDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Purpose { get; set; } = string.Empty;
}

public class ReviewPettyCashDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public decimal? ApprovedAmount { get; set; }
}

public class PettyCashReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? ReviewComments { get; set; }
    public string? ReviewedByManagerId { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? ReviewedByCfoId { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
