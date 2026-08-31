using ComplianceService.Core.DTOs.Common;

namespace ComplianceService.Core.DTOs.Icm;

public class IcsaFilterParameters : PaginationParameters
{
    public string? RelatedPartyId { get; set; }
}

public class IcsaReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RelatedPartyId { get; set; } = string.Empty;
    public string? RelatedPartyName { get; set; }
    public string Scope { get; set; } = string.Empty;
    public decimal RechargeRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateIcsaDto
{
    public string RelatedPartyId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal RechargeRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpdateIcsaDto
{
    public string RelatedPartyId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal RechargeRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
