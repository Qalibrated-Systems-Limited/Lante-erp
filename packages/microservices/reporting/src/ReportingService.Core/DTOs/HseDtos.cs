namespace ReportingService.Core.DTOs;

// Mirrors HSEService.Core.DTOs shapes returned by GET /api/v1/hse-dashboard and
// /api/v1/hse-incidents. HSEService.Api has no JsonStringEnumConverter registered, so enums
// serialize as integers on the wire — the enums below match HSEService.Core.Enums member order
// exactly so the integer values deserialize into the correct named members here.

public enum IncidentType { NearMiss = 0, FirstAid = 1, MedicalTreatment = 2, LostTimeInjury = 3, PositiveObservation = 4 }
public enum IncidentSeverity { None = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum IncidentStatus { Open = 0, UnderInvestigation = 1, CorrectiveActionPending = 2, Closed = 3 }
public enum CorrectiveActionStatus { Open = 0, InProgress = 1, Completed = 2, Overdue = 3 }

public class HseDashboardDto
{
    public decimal Trir { get; set; }
    public decimal Ltif { get; set; }
    public int NearMissCount { get; set; }
    public int TotalIncidentsYtd { get; set; }
    public int OpenCorrectiveActions { get; set; }
    public int OverdueCorrectiveActions { get; set; }
    public int RamsPendingApproval { get; set; }
    public int TrainingCertificatesExpiringSoon { get; set; }
    public int StatutoryInspectionsDueSoon { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class EnvIncidentDto
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string? NemaRef { get; set; }
    public bool NemaNotificationRequired { get; set; }
    public DateTime? NotifiedAt { get; set; }
}

public class CorrectiveActionReadDto
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public CorrectiveActionStatus Status { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class HseIncidentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string ReportedByUserId { get; set; } = string.Empty;
    public string? ReportedByName { get; set; }
    public string Description { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public EnvIncidentDto? EnvIncident { get; set; }
    public List<CorrectiveActionReadDto> CorrectiveActions { get; set; } = new();
}
