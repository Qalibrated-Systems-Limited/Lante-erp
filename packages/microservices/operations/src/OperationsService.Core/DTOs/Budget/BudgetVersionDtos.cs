using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Budget;

// ── Detailed, approvable budget ───────────────────────────────────────────────

public class BudgetVersionDto
{
    public string Id        { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public int    VersionNo { get; set; }
    public string Status    { get; set; } = string.Empty;
    public string? RevisionReason { get; set; }

    public decimal TotalPlanned { get; set; }
    public decimal TotalQuoted  { get; set; }
    /// <summary>Quoted less planned cost — the margin this budget is designed to make.</summary>
    public decimal PlannedMargin { get; set; }
    public decimal PlannedMarginPct { get; set; }

    public string?   SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string?   ApprovedBy  { get; set; }
    public DateTime? ApprovedAt  { get; set; }
    public string?   RejectedBy  { get; set; }
    public DateTime? RejectedAt  { get; set; }
    public string?   RejectionReason { get; set; }
    public DateTime? SupersededAt { get; set; }

    public List<BudgetLineDetailDto> Lines { get; set; } = new();
}

public class BudgetLineDetailDto
{
    public string Id          { get; set; } = string.Empty;
    public string Category    { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public decimal? Quantity     { get; set; }
    public decimal? UnitCostRate { get; set; }
    public string?  Unit         { get; set; }
    public string?  ContractRateId { get; set; }
    public string?  ContractRateCode { get; set; }

    public decimal PlannedAmount { get; set; }
    public decimal QuotedAmount  { get; set; }
    public decimal ActualAmount  { get; set; }
    public decimal PlannedMargin { get; set; }
    public decimal ActualMargin  { get; set; }
}

public class CreateBudgetVersionDto
{
    /// <summary>Required from v2 onward — a revision without a stated reason defeats the approval step.</summary>
    public string? RevisionReason { get; set; }
    /// <summary>Optional: copy the lines of the current approved version as a starting point.</summary>
    public bool CopyFromApproved { get; set; } = true;
}

public class UpsertBudgetLineDto
{
    public string? Id { get; set; }
    public BudgetCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>When quantity and a rate are supplied, PlannedAmount is computed rather than typed.</summary>
    public decimal? Quantity     { get; set; }
    public decimal? UnitCostRate { get; set; }
    public RateUnit? Unit        { get; set; }
    /// <summary>Price from the rate card instead of typing rates; fills cost and quoted together.</summary>
    public string? ContractRateId { get; set; }

    public decimal? PlannedAmount { get; set; }
    public decimal? QuotedAmount  { get; set; }
}

public class RejectBudgetDto
{
    public string Reason { get; set; } = string.Empty;
}

// ── Contract rate card ────────────────────────────────────────────────────────

public class ContractRateDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ClientRate { get; set; }
    public decimal CostRate { get; set; }
    public decimal MarginPerUnit { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertContractRateDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BudgetCategory Category { get; set; }
    public RateUnit Unit { get; set; } = RateUnit.Hour;
    public decimal ClientRate { get; set; }
    public decimal CostRate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
}

// ── Quote vs spend ────────────────────────────────────────────────────────────

/// <summary>
/// PR1 — what we quoted the client against what the job is costing. Reported per category and per
/// line, because a project can be on budget in total while one category quietly bleeds.
/// </summary>
public class ProjectCommercialsDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Headline contract value on the project record.</summary>
    public decimal ContractValue { get; set; }
    /// <summary>Sum of quoted amounts on the approved budget's lines.</summary>
    public decimal QuotedTotal { get; set; }
    public decimal PlannedCost { get; set; }
    /// <summary>The approved baseline, if one exists — what variance is measured against.</summary>
    public decimal? BaselineBudget { get; set; }
    public decimal Committed  { get; set; }
    public decimal ActualCost { get; set; }

    /// <summary>Actual + committed. What the job is really on the hook for.</summary>
    public decimal Exposure { get; set; }
    /// <summary>Quoted less exposure — margin as things actually stand.</summary>
    public decimal ProjectedMargin { get; set; }
    public decimal ProjectedMarginPct { get; set; }
    /// <summary>Exposure less the approved baseline. Positive means over what was approved.</summary>
    public decimal? VarianceToBaseline { get; set; }

    public List<CommercialCategoryDto> ByCategory { get; set; } = new();
    public List<BudgetLineDetailDto>   Lines      { get; set; } = new();
}

public class CommercialCategoryDto
{
    public string  Category    { get; set; } = string.Empty;
    public decimal Quoted      { get; set; }
    public decimal Planned     { get; set; }
    public decimal Actual      { get; set; }
    public decimal Margin      { get; set; }
    /// <summary>Actual as a share of planned. Over 100 means the category has overrun its budget.</summary>
    public decimal BurnPct     { get; set; }
}
