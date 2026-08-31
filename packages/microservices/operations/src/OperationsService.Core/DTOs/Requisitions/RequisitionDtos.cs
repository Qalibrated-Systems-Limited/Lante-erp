using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Requisitions;

public class CreateRequisitionDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public RequisitionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Justification { get; set; }
    public List<RequisitionLineItemDto> LineItems { get; set; } = [];
}

public class UpdateRequisitionDto
{
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? Justification { get; set; }
    public List<RequisitionLineItemDto>? LineItems { get; set; }
}

public class RequisitionLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ReviewRequisitionDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public decimal? ApprovedAmount { get; set; }
}

public class RequisitionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Justification { get; set; }
    public string? ManagerComments { get; set; }
    public string? CfoComments { get; set; }
    public string? ReviewedByManagerId { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }
    public string? ReviewedByCfoId { get; set; }
    public DateTime? CfoReviewedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<RequisitionLineItemDto> LineItems { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class RequisitionFilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string? AssignmentId { get; set; }
    public string? RequestedByUserId { get; set; }
    public int? Status { get; set; }
    public int? Type { get; set; }
    public bool SortDescending { get; set; } = true;
}
