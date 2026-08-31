namespace ReportingService.Core.DTOs;

// Mirrors OperationsService.Core.DTOs shapes returned by GET /api/v1/projects/... endpoints.

public class ProjectReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientReference { get; set; }
    public string? TenderReference { get; set; }
    public string? ScopeSummary { get; set; }
    public string? Notes { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string ProjectManagerId { get; set; } = string.Empty;
    public string? CrmLeadId { get; set; }
    public decimal ContractValue { get; set; }
    public decimal PlannedBudget { get; set; }
    public decimal ActualCost { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpectedEndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MilestoneCount { get; set; }
    public int TaskCount { get; set; }
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
    public string RecordedByUserId { get; set; } = string.Empty;
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
    public decimal Remaining { get; set; }
    public decimal UtilizationPercent { get; set; }
    public List<BudgetLineReadDto> Lines { get; set; } = new();
    public List<MilestoneBudgetDto> MilestoneBreakdown { get; set; } = new();
    public List<CostEntryReadDto> CostEntries { get; set; } = new();
}
