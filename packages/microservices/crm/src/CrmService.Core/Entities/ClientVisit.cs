namespace CrmService.Core.Entities;

/// <summary>P7 — CLIENT_VISIT. Logged client visits with GPS + next action (feeds visit-target monitoring).</summary>
public class ClientVisit : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    public string Purpose { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public string? NextAction { get; set; }
    public double? GpsLat { get; set; }
    public double? GpsLng { get; set; }
}
