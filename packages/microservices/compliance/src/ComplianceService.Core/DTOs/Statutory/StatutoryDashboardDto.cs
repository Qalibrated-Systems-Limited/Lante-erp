namespace ComplianceService.Core.DTOs.Statutory;

// STAT-010: one-screen view of all upcoming statutory deadlines sorted by due date, RAG-coded.
// Merges recurring StatutoryDeadline rows with RegulatoryLicence expiries, AnnualReturn due
// dates and the TaxComplianceCert expiry into one unified, sorted list — the single-screen view
// the requirement asks for, rather than four separate registers.
public class StatutoryCalendarItemDto
{
    public string SourceType { get; set; } = string.Empty; // "Obligation" | "Licence" | "AnnualReturn" | "Tcc"
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
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
