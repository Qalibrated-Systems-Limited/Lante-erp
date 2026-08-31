namespace SubcontractsService.Core.DTOs.Awards;

// SUB-004/SUB-005: award record + hard mobilization gate.
public class SubcontractAwardReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SubcontractorId { get; set; } = string.Empty;
    public string? SubcontractorName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public decimal Value { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SignedAgreementUrl { get; set; }
    public bool RamsApproved { get; set; }
    public DateTime? MobilizationActivatedAt { get; set; }
}

public class CreateSubcontractAwardDto
{
    public string SubcontractorId { get; set; } = string.Empty;
    public string? SubcontractorName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public decimal Value { get; set; }
    public string? SignedAgreementUrl { get; set; }
}

public class UpdateAwardStatusDto
{
    public Enums.AwardStatus Status { get; set; }
}

// Core-field edit — Status/RamsApproved/MobilizationActivatedAt stay workflow-controlled (see
// UpdateAwardStatusDto / ActivateMobilization).
public class UpdateSubcontractAwardDto
{
    public string SubcontractorId { get; set; } = string.Empty;
    public string? SubcontractorName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public decimal Value { get; set; }
    public string? SignedAgreementUrl { get; set; }
}
