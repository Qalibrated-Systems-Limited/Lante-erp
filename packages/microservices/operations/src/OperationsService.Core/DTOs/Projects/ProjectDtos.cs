using OperationsService.Core.DTOs.Approvals;
using OperationsService.Core.DTOs.Milestones;
using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Projects;

public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientReference { get; set; }
    public string? TenderReference { get; set; }
    public string? ScopeSummary { get; set; }
    public string? Notes { get; set; }
    public ProjectType Type { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public string DepartmentId { get; set; } = string.Empty;
    public string? ProcessOwnerId { get; set; }
    public string? CrmLeadId { get; set; }
    public string? ClientId { get; set; }
    public decimal ContractValue { get; set; }
    public decimal PlannedBudget { get; set; }
    public decimal LdRatePerDay { get; set; }
    public decimal LdCapPct { get; set; } = 0.10m;
    public DateTime StartDate { get; set; }
    public DateTime ExpectedEndDate { get; set; }
}

public class UpdateProjectDto
{
    public string? Name { get; set; }
    public string? ClientName { get; set; }
    public string? ClientReference { get; set; }
    public string? TenderReference { get; set; }
    public string? ScopeSummary { get; set; }
    public string? Notes { get; set; }
    public string? ProcessOwnerId { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public decimal? ContractValue { get; set; }
    public decimal? PlannedBudget { get; set; }
    public decimal? LdRatePerDay { get; set; }
    public decimal? LdCapPct { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
}

public class ProjectReadDto
{
    // PR1 — the approved baseline and the signed contract.
    public decimal?  BaselineBudget { get; set; }
    public DateTime? BaselineSetAt  { get; set; }
    public string?   ContractAttachmentId { get; set; }

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
    public string? ProcessOwnerId { get; set; }
    public string? CrmLeadId { get; set; }
    public string? ClientId { get; set; }
    public decimal ContractValue { get; set; }
    public decimal PlannedBudget { get; set; }
    public decimal ActualCost { get; set; }
    public decimal Committed { get; set; }
    public decimal LdRatePerDay { get; set; }
    public decimal LdCapPct { get; set; }
    public bool    BudgetLocked { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpectedEndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MilestoneCount { get; set; }
    public int TaskCount { get; set; }
    // Nested milestones (each with its tasks) so the project-detail page renders them without extra calls.
    public List<MilestoneReadDto> Milestones { get; set; } = new();
    // Approval requests (MD + Finance) so the detail view can render + process them.
    public List<ProjectApprovalReadDto> Approvals { get; set; } = new();
}

public class ProjectFilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string? Search { get; set; }
    public string? DepartmentId { get; set; }
    public List<string>? DepartmentIds { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public bool SortDescending { get; set; } = true;
    public string? MemberUserId { get; set; }
}
