using HSEService.Core.DTOs.Common;
using HSEService.Core.Enums;

namespace HSEService.Core.DTOs.Inspections;

public class StatutoryInspectionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public string? InspectorName { get; set; }
    public DateTime? LastInspectedAt { get; set; }
    public DateTime DueDate { get; set; }
    public InspectionStatus Status { get; set; }
    public string? CertificateUrl { get; set; }
}

public class CreateStatutoryInspectionDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}

// Core-field edit — Status/InspectorName/CertificateUrl stay workflow-controlled via RecordInspectionResultDto.
public class UpdateStatutoryInspectionDto
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}

public class RecordInspectionResultDto
{
    public InspectionStatus Status { get; set; }
    public string? InspectorName { get; set; }
    public string? CertificateUrl { get; set; }
    public DateTime NextDueDate { get; set; }
}

public class StatutoryInspectionFilterParameters : PaginationParameters
{
    public string? SiteId { get; set; }
}
