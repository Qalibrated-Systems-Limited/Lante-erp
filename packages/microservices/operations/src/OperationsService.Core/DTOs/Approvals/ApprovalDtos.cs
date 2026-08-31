using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Approvals;

public class SubmitProjectForApprovalDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ReviewProjectApprovalDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public decimal? AllocatedBudget { get; set; }
}

public class ProjectApprovalReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ApprovalType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public string? Comments { get; set; }
    public decimal? AllocatedBudget { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
