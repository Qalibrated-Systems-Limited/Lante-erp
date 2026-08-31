namespace SubcontractsService.Core.Entities;

// SUB-001: Approved Subcontractor Register (ASR) — the canonical subcontractor record, searchable
// by trade category. HSE previously kept its own local stub of this (SafetyScore/RamsSubmitted/
// Prequalified) — that data now lives here, and HSE calls out to this service instead of owning
// a second, drifting copy (see the migration noted in HSEService's SubcontractorsController).
public class Subcontractor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string TradeCategory { get; set; } = string.Empty;
    public decimal? PqqScore { get; set; }
    public DateTime? InsuranceExpiry { get; set; }
    public DateTime? TccExpiry { get; set; }

    // HSE prequalification fields (formerly HSEService.Core.Entities.Subcontractor).
    public decimal SafetyScore { get; set; }
    public bool RamsSubmitted { get; set; }
    public bool Prequalified { get; set; }

    // SUB-006: below 6.0 on a performance scorecard triggers watch list.
    public decimal? LatestPerformanceScore { get; set; }
    public bool WatchListed { get; set; }

    // SUB-008: insurance or TCC expiring within 30 days restricts the subcontractor from new
    // awards until renewed (SubcontractsAlertsBackgroundService sets this; SubcontractAwardsController
    // enforces it).
    public bool IsRestricted { get; set; }

    // SUB-009: prompts a COI declaration when linked to a Director/Shareholder/employee relationship.
    public bool HasDeclaredRelationship { get; set; }
    public string? RelationshipDetails { get; set; }

    public string? Notes { get; set; }
}
