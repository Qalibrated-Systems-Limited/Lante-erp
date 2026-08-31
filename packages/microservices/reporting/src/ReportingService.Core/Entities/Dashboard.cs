namespace ReportingService.Core.Entities;

// RPT-008: a named collection of widgets. IsDefault marks the tenant's landing dashboard;
// OwnerUserId null = tenant-wide (visible to anyone with reports.view), set = personal.
public class Dashboard : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public string? OwnerUserId { get; set; }
}
