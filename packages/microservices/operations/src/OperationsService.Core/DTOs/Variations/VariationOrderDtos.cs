using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Variations;

public class VariationOrderLineDto
{
    public string? Id { get; set; }
    public string  Description { get; set; } = string.Empty;
    public decimal Quantity  { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Amount    { get; set; }   // read-only; computed = Quantity × UnitPrice
}

public class CreateVariationOrderDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title     { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reason      { get; set; }
    public bool IsBillable { get; set; } = true;
    public BudgetCategory BudgetCategory { get; set; } = BudgetCategory.Other;
    public decimal VatRate { get; set; } = 0.16m;
    public List<VariationOrderLineDto> Lines { get; set; } = new();
}

public class UpdateVariationOrderDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Reason { get; set; }
    public bool? IsBillable { get; set; }
    public BudgetCategory? BudgetCategory { get; set; }
    public decimal? VatRate { get; set; }
    public List<VariationOrderLineDto>? Lines { get; set; }
}

public class ReviewVariationOrderDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
}

public class ClientApproveVariationOrderDto
{
    public string ClientApprovedBy { get; set; } = string.Empty;   // client representative name
}

public class VariationOrderReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public string BudgetCategory { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? RequestedBy { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public string? ClientApprovedBy { get; set; }
    public DateTime? ClientApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? BudgetLineId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<VariationOrderLineDto> Lines { get; set; } = new();
}
