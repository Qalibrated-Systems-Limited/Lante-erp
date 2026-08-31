using HSEService.Core.Enums;

namespace HSEService.Core.Entities;

// HSE-006: scaffolding, lifting equipment, pressure vessels — legal inspection due dates.
public class StatutoryInspection : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public string Equipment { get; set; } = string.Empty;
    public string? InspectorName { get; set; }
    public DateTime? LastInspectedAt { get; set; }
    public DateTime DueDate { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.Scheduled;
    public string? CertificateUrl { get; set; }
}
