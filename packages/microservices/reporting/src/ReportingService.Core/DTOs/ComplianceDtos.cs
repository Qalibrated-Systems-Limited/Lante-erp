namespace ReportingService.Core.DTOs;

// Mirrors ComplianceService.Core.DTOs shapes returned by GET /api/v1/compliance-dashboard,
// /api/v1/statutory-dashboard and /api/v1/compliance-policies.

public class ComplianceDashboardDto
{
    public int GiftsFlaggedPendingReview { get; set; }
    public int CoiDeclarationsOutstandingThisYear { get; set; }
    public int WhistleblowerCasesOpen { get; set; }
    public int DsrDueSoon { get; set; }
    public int DsrOverdue { get; set; }
    public int DataBreachesPendingOdpcNotification { get; set; }
    public int LicencesExpiringSoon { get; set; }
    public int LicencesExpired { get; set; }
    public int AbcTrainingDueSoon { get; set; }
    public int RelatedPartyTransactionsUnreported { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class StatutoryCalendarItemDto
{
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string Rag { get; set; } = "Green";
    public bool IsOverdue { get; set; }
    public string? OwnerName { get; set; }
}

public class StatutoryDashboardDto
{
    public int GreenCount { get; set; }
    public int AmberCount { get; set; }
    public int RedCount { get; set; }
    public List<StatutoryCalendarItemDto> UpcomingDeadlines { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

// Note: there is no total-employee-count / headcount field anywhere in ComplianceService, so
// AcknowledgedCount is returned as-is per the spec — computing an acknowledgement rate would
// require inventing a denominator, which we deliberately do not do.
public class PolicyReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string? FileUrl { get; set; }
    public int AcknowledgedCount { get; set; }
}
