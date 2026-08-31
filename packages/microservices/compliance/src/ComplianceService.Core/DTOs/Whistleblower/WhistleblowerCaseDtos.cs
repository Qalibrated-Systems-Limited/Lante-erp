using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Whistleblower;

public class WhistleblowerCaseReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public bool Anonymous { get; set; }
    public string Summary { get; set; } = string.Empty;
    public WhistleblowerStatus Status { get; set; }
    public string? Outcome { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWhistleblowerCaseDto
{
    public bool Anonymous { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class UpdateWhistleblowerCaseDto
{
    public WhistleblowerStatus Status { get; set; }
    public string? Outcome { get; set; }
}

// Core-field edit — Status/Outcome stay investigation-controlled (see UpdateWhistleblowerCaseDto).
// RefNo/SubmittedByUserId are set at creation and never change.
public class UpdateWhistleblowerCaseDetailsDto
{
    public bool Anonymous { get; set; }
    public string Summary { get; set; } = string.Empty;
}
