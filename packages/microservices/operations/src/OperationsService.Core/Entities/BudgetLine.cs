using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class BudgetLine : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>PR1 — the budget version this line belongs to. Null for lines created before
    /// budgets were versioned; those are treated as belonging to the project's baseline.</summary>
    public string? BudgetVersionId { get; set; }

    public BudgetCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;

    // PR1 — detailed pricing. PlannedAmount stays the authoritative figure (it is what the rest of
    // the module already sums); quantity and rate explain how it was arrived at, and recompute it
    // when either changes.
    public decimal? Quantity     { get; set; }
    public decimal? UnitCostRate { get; set; }   // our cost per unit
    public RateUnit? Unit        { get; set; }
    /// <summary>Rate-card row this line was priced from, when it came from one.</summary>
    public string? ContractRateId { get; set; }

    public decimal PlannedAmount { get; set; }

    /// <summary>What the client is charged for this line. Compared against PlannedAmount (our cost)
    /// and ActualAmount (what we have spent) to give margin per line.</summary>
    public decimal QuotedAmount { get; set; }

    public decimal ActualAmount { get; set; }

    /// <summary>Quoted less our planned cost. Negative means the line is planned to lose money.</summary>
    public decimal PlannedMargin => QuotedAmount - PlannedAmount;
    /// <summary>Quoted less what we have actually spent so far.</summary>
    public decimal ActualMargin  => QuotedAmount - ActualAmount;

    public Project Project { get; set; } = null!;
    public BudgetVersion? BudgetVersion { get; set; }
    public ContractRate?  ContractRate  { get; set; }
    public ICollection<CostEntry> CostEntries { get; set; } = new List<CostEntry>();
}
