using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientReference { get; set; }
    public string? TenderReference { get; set; }
    public string? ScopeSummary { get; set; }
    public string? Notes { get; set; }
    public ProjectType Type { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public string DepartmentId { get; set; } = string.Empty;
    public string ProjectManagerId { get; set; } = string.Empty;
    // O1 — single accountable owner for the project (DFD "process owner"), distinct from the PM who
    // runs day-to-day delivery. Used for escalation/alert routing in O2/O3.
    public string? ProcessOwnerId { get; set; }
    public string? CrmLeadId { get; set; }
    // Strong link to the CRM CUSTOMER (system-of-record) — set when a project is created from a won deal.
    public string? ClientId { get; set; }
    public decimal ContractValue { get; set; }
    public decimal PlannedBudget { get; set; }
    public decimal ActualCost { get; set; }
    // O2 — committed-but-not-yet-spent (POs / subcontracts). Burn exposure = ActualCost + Committed.
    public decimal Committed { get; set; }
    // O3 — liquidated-damages contract terms: accrual rate per day of delay and the cap (fraction of
    // the milestone value). LdRatePerDay = 0 means LD is not applied. Cap defaults to 10%.
    public decimal LdRatePerDay { get; set; }
    public decimal LdCapPct     { get; set; } = 0.10m;
    public DateTime StartDate { get; set; }
    public DateTime ExpectedEndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    // O1 — set when the project is activated (MD/Finance-approved → Active). The MD-activation gate:
    // no expenditure or timesheets may post until the project is Active.
    public DateTime? ActivatedAt { get; set; }

    // PR1 — the budget as approved, captured when a BudgetVersion is approved. PlannedBudget may be
    // revised afterwards; this is what variance is measured against, and it is never overwritten by
    // an edit — only by a newly approved budget version.
    public decimal?  BaselineBudget { get; set; }
    public DateTime? BaselineSetAt  { get; set; }

    /// <summary>PR1 — the signed contract, uploaded at project creation. Points at an Attachment row
    /// (EntityType "Project"); the file lives with every other attachment rather than in a second
    /// storage path.</summary>
    public string? ContractAttachmentId { get; set; }

    // O2 — budget-burn alert bookkeeping (one alert per threshold, fired by the background sweep) and
    // the hard block: BudgetLocked is set at 100% burn and blocks any further expenditure until the
    // budget is revised.
    public DateTime? Alert80SentAt  { get; set; }
    public DateTime? Alert90SentAt  { get; set; }
    public DateTime? Alert95SentAt  { get; set; }
    public DateTime? Alert100SentAt { get; set; }
    public bool      BudgetLocked   { get; set; }

    // O9 — set once the background sweep raises a PROGRAM_OVERRUN_NOTICE (>14 days past expected end).
    public DateTime? OverrunNoticedAt { get; set; }

    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public ICollection<ProjectApproval> Approvals { get; set; } = new List<ProjectApproval>();
    public ICollection<BudgetLine> BudgetLines { get; set; } = new List<BudgetLine>();
    public ICollection<BudgetVersion> BudgetVersions { get; set; } = new List<BudgetVersion>();
    public ICollection<ContractRate> ContractRates { get; set; } = new List<ContractRate>();
    public ICollection<CostEntry> CostEntries { get; set; } = new List<CostEntry>();
    public ICollection<ProjectResource> Resources { get; set; } = new List<ProjectResource>();
    public ICollection<ProjectHistory> History { get; set; } = new List<ProjectHistory>();
}
