using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Icm;

public class RelatedPartyReadDto
{
    public string Id { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string RegNo { get; set; } = string.Empty;
    public PartyRelationship Relationship { get; set; }
    public string? Notes { get; set; }
}

public class CreateRelatedPartyDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string RegNo { get; set; } = string.Empty;
    public PartyRelationship Relationship { get; set; }
    public string? Notes { get; set; }
}

public class UpdateRelatedPartyDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string RegNo { get; set; } = string.Empty;
    public PartyRelationship Relationship { get; set; }
    public string? Notes { get; set; }
}
