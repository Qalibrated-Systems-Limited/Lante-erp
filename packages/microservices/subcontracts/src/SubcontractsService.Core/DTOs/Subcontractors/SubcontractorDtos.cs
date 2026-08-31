namespace SubcontractsService.Core.DTOs.Subcontractors;

// SUB-001: Approved Subcontractor Register (ASR).
public class SubcontractorReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TradeCategory { get; set; } = string.Empty;
    public decimal? PqqScore { get; set; }
    public DateTime? InsuranceExpiry { get; set; }
    public DateTime? TccExpiry { get; set; }
    public decimal SafetyScore { get; set; }
    public bool RamsSubmitted { get; set; }
    public bool Prequalified { get; set; }
    public decimal? LatestPerformanceScore { get; set; }
    public bool WatchListed { get; set; }
    public bool IsRestricted { get; set; }
    public bool HasDeclaredRelationship { get; set; }
    public string? RelationshipDetails { get; set; }
    public string? Notes { get; set; }
}

public class CreateSubcontractorDto
{
    public string Name { get; set; } = string.Empty;
    public string TradeCategory { get; set; } = string.Empty;
    public DateTime? InsuranceExpiry { get; set; }
    public DateTime? TccExpiry { get; set; }
    public bool HasDeclaredRelationship { get; set; }
    public string? RelationshipDetails { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSubcontractorDto
{
    public string Name { get; set; } = string.Empty;
    public string TradeCategory { get; set; } = string.Empty;
    public DateTime? InsuranceExpiry { get; set; }
    public DateTime? TccExpiry { get; set; }
    public decimal SafetyScore { get; set; }
    public bool RamsSubmitted { get; set; }
    public bool HasDeclaredRelationship { get; set; }
    public string? RelationshipDetails { get; set; }
    public string? Notes { get; set; }
}
