using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// ICM-001: register of sister companies / affiliates that intercompany services agreements
// and dual-ledger recharges are posted against.
public class RelatedParty : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string RegNo { get; set; } = string.Empty;
    public PartyRelationship Relationship { get; set; }
    public string? Notes { get; set; }
}
