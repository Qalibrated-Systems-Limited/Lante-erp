using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Statutory;

public class StatutoryObligationReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public ObligationFrequency Frequency { get; set; }
    public int StatutoryDay { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStatutoryObligationDto
{
    public string Name { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public ObligationFrequency Frequency { get; set; }
    public int StatutoryDay { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
}

public class UpdateStatutoryObligationDto
{
    public string Name { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public ObligationFrequency Frequency { get; set; }
    public int StatutoryDay { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
}
