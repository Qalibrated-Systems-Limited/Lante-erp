namespace ProcurementService.Core.DTOs.Requisitions;

// ── Create / update ──
public class CreatePrLineDto
{
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
}

public class CreatePrDto
{
    public string DepartmentId { get; set; } = string.Empty;
    public string? BudgetId { get; set; }
    public string? BudgetName { get; set; }
    public string? Justification { get; set; }
    public List<CreatePrLineDto> Lines { get; set; } = new();
}

// ── Reads ──
public class PrLineDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class PrReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PrNumber { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public string? BudgetId { get; set; }
    public string? BudgetName { get; set; }
    public string? Justification { get; set; }
    public decimal TotalEstimated { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? SlaDueAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? PurchaseOrderId { get; set; }
    /// <summary>P8 — true when this requisition ratifies an emergency purchase after the fact.</summary>
    public bool IsPostHoc { get; set; }
    public string? LinkedEmergencyPoId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PrLineDto> Lines { get; set; } = new();
}

public class PrSummaryRowDto
{
    public string Id { get; set; } = string.Empty;
    public string PrNumber { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public decimal TotalEstimated { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SlaDueAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PrFilterParams
{
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public bool? OverdueOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record PrListResult(List<PrSummaryRowDto> Items, int Total);

// ── Actions ──
public class ReviewPrDto
{
    public bool Approve { get; set; }
    public string? Reason { get; set; }   // mandatory on reject
}

public class PrSummaryDto
{
    public int Total { get; set; }
    public int Draft { get; set; }
    public int PendingDeptHead { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Overdue { get; set; }
    public decimal PendingValue { get; set; }
}

public record PrActionResult(string Status, string Message);
