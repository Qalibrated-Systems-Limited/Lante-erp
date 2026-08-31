using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class CostEntry : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string? BudgetLineId { get; set; }
    public string? MilestoneId { get; set; }
    public string? AssignmentId { get; set; }
    public BudgetCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    // O2 (PROJECT_EXPENDITURE) — expenditure should be logged same-day; a backdated entry (EntryDate
    // before today) is flagged and requires a Finance-Manager approval reference. CreatedAt remains the
    // immutable ERP timestamp of when it was actually recorded.
    public bool    IsBackdated   { get; set; }
    public string? FmApprovalRef { get; set; }

    public Project Project { get; set; } = null!;
    public BudgetLine? BudgetLine { get; set; }
    public Milestone? Milestone { get; set; }
}
