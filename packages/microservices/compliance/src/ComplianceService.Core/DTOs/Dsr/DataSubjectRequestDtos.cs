using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Dsr;

public class DataSubjectRequestReadDto
{
    public string Id { get; set; } = string.Empty;
    public DsrType Type { get; set; }
    public string RequestorName { get; set; } = string.Empty;
    public string? RequestorContact { get; set; }
    public DateTime ReceivedOn { get; set; }
    public DateTime DueBy { get; set; }
    public DsrStatus Status { get; set; }
    public DateTime? CompletedOn { get; set; }
    public string? Notes { get; set; }
}

public class CreateDataSubjectRequestDto
{
    public DsrType Type { get; set; }
    public string RequestorName { get; set; } = string.Empty;
    public string? RequestorContact { get; set; }
    public DateTime ReceivedOn { get; set; }
}

public class UpdateDataSubjectRequestDto
{
    public DsrStatus Status { get; set; }
    public string? Notes { get; set; }
}

// Core-field edit — Status/Notes/CompletedOn stay workflow-controlled (see UpdateDataSubjectRequestDto).
// DueBy is recomputed from ReceivedOn, same 30-day rule as creation.
public class UpdateDataSubjectRequestDetailsDto
{
    public DsrType Type { get; set; }
    public string RequestorName { get; set; } = string.Empty;
    public string? RequestorContact { get; set; }
    public DateTime ReceivedOn { get; set; }
}
