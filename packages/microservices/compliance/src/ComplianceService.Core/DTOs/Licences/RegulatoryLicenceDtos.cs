using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Licences;

public class RegulatoryLicenceFilterParameters : PaginationParameters
{
    public LicenceType? Type { get; set; }
}

public class RegulatoryLicenceReadDto
{
    public string Id { get; set; } = string.Empty;
    public LicenceType Type { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string? LicenceNumber { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int AlertDays { get; set; }
    public string? RenewalRequirements { get; set; }
}

public class CreateRegulatoryLicenceDto
{
    public LicenceType Type { get; set; } = LicenceType.Other;
    public string Authority { get; set; } = string.Empty;
    public string? LicenceNumber { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int AlertDays { get; set; } = 90;
    public string? RenewalRequirements { get; set; }
}

public class UpdateRegulatoryLicenceDto
{
    public LicenceType Type { get; set; } = LicenceType.Other;
    public string Authority { get; set; } = string.Empty;
    public string? LicenceNumber { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int AlertDays { get; set; } = 90;
    public string? RenewalRequirements { get; set; }
}

public class RenewRegulatoryLicenceDto
{
    public string? LicenceNumber { get; set; }
    public DateTime IssuedOn { get; set; }
    public DateTime NewExpiryDate { get; set; }
}
