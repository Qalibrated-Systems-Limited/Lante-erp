using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Budget;

public class CreateBudgetLineDto
{
    public string ProjectId { get; set; } = string.Empty;
    public BudgetCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
}

public class UpdateBudgetLineDto
{
    public string? Description { get; set; }
    public decimal? PlannedAmount { get; set; }
}

public class BudgetLineReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCostEntryDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string? BudgetLineId { get; set; }
    public string? MilestoneId { get; set; }
    public string? AssignmentId { get; set; }
    public BudgetCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    // O2 — expenditure date; if before today the entry is backdated and requires an FM approval ref.
    public DateTime? ExpenditureDate { get; set; }
    public string?   FmApprovalRef   { get; set; }
}

public class CostEntryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? BudgetLineId { get; set; }
    public string? MilestoneId { get; set; }
    public string? MilestoneTitle { get; set; }
    public string? AssignmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime EntryDate { get; set; }
    public bool     IsBackdated   { get; set; }
    public string?  FmApprovalRef { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ProjectAlertLogReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public int ThresholdPct { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal BurnPct { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MilestoneBudgetDto
{
    public string MilestoneId { get; set; } = string.Empty;
    public string MilestoneTitle { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
}

public class BudgetSummaryDto
{
    public string ProjectId { get; set; } = string.Empty;
    public decimal PlannedBudget { get; set; }
    public decimal TotalEstimated { get; set; }
    public decimal ActualCost { get; set; }
    public decimal Committed { get; set; }
    public decimal Remaining { get; set; }
    public decimal UtilizationPercent { get; set; }
    public bool    BudgetLocked { get; set; }
    public List<BudgetLineReadDto> Lines { get; set; } = [];
    public List<MilestoneBudgetDto> MilestoneBreakdown { get; set; } = [];
    public List<CostEntryReadDto> CostEntries { get; set; } = [];
}
