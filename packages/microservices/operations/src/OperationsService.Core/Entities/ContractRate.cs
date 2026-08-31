using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR1 — a priced line on the project's contract rate card.
///
/// Carries two rates deliberately: <see cref="ClientRate"/> is what the client is charged, and
/// <see cref="CostRate"/> is what it costs us to deliver. Holding both is what lets the module answer
/// "quoted vs spending" per line rather than only in total — margin is a property of the rate, not
/// something recomputed from a lump sum after the fact.
/// </summary>
public class ContractRate : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Short code used on budget lines and quotes, e.g. "TECH-HR" or "CAL-NAWI".</summary>
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BudgetCategory Category { get; set; }
    public RateUnit Unit { get; set; } = RateUnit.Hour;

    public decimal ClientRate { get; set; }
    public decimal CostRate   { get; set; }

    /// <summary>Contracted rates change mid-project; the old row is deactivated rather than edited so
    /// budget lines priced under it still explain their own numbers.</summary>
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo  { get; set; }
    public bool IsActive { get; set; } = true;

    public Project Project { get; set; } = null!;

    /// <summary>Margin per unit. Negative means the line is priced below cost.</summary>
    public decimal MarginPerUnit => ClientRate - CostRate;
}
