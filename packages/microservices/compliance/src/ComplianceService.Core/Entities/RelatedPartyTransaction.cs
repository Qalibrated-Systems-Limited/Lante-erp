using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// COMP-010: all transactions with shareholders, directors, or affiliates flagged
// and reported. Scoped to the flag+report register described in the requirement —
// the fuller Inter-Company Services Agreement / dual-ledger recharge system (ICM
// cluster in the ERD companion doc) is a separate, larger Finance-module feature
// and intentionally not built here.
public class RelatedPartyTransaction : BaseEntity
{
    public string PartyName { get; set; } = string.Empty;
    public RelationshipType RelationshipType { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public bool Flagged { get; set; } = true;
    public bool Reported { get; set; }
    public DateTime? ReportedAt { get; set; }
}
