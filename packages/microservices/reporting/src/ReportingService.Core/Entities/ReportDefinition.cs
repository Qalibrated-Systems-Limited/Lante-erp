using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-001: registers each report ReportsController already knows how to build (Key matches the
// controller's route segment, e.g. "management-accounts") so it can be scheduled/delivered.
public class ReportDefinition : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReportCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
}
