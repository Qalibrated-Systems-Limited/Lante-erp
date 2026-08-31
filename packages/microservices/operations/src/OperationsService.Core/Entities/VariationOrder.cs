using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O7 — VARIATION_ORDER (P6): a change to a project's scope/value. Flows Draft → MD approval →
/// client approval; on client approval it auto-updates the project's contract value + planned budget
/// (a new BUDGET_LINE) and, if billable, raises a Finance invoice.
/// </summary>
public class VariationOrder : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string Number    { get; set; } = string.Empty;   // VO-{year}-{seq}
    public string Title     { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reason      { get; set; }

    public VariationOrderStatus Status { get; set; } = VariationOrderStatus.Draft;
    public bool IsBillable { get; set; } = true;
    public BudgetCategory BudgetCategory { get; set; } = BudgetCategory.Other;

    public decimal Subtotal    { get; set; }
    public decimal VatRate     { get; set; } = 0.16m;
    public decimal VatAmount   { get; set; }
    public decimal TotalAmount { get; set; }

    public string?   RequestedBy      { get; set; }
    public string?   MdApprovedBy     { get; set; }
    public DateTime? MdApprovedAt     { get; set; }
    public string?   ClientApprovedBy { get; set; }   // client representative name
    public DateTime? ClientApprovedAt { get; set; }
    public string?   RejectionReason  { get; set; }

    // Set once the approval side-effects (contract value, budget line, invoice) have been applied.
    public DateTime? AppliedAt      { get; set; }
    public string?   BudgetLineId   { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<VariationOrderLine> Lines { get; set; } = new List<VariationOrderLine>();
}
