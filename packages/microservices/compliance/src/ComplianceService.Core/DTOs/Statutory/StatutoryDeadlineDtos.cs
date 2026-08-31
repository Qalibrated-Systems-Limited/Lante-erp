using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Statutory;

public class StatutoryDeadlineFilterParameters : PaginationParameters
{
    public string? ObligationId { get; set; }
}

public class StatutoryDeadlineReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ObligationId { get; set; } = string.Empty;
    public string? ObligationName { get; set; }
    public string? Authority { get; set; }
    public DateTime DueDate { get; set; }
    public StatutoryDeadlineStatus Status { get; set; }
    public DateTime? FiledOn { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    /// <summary>"Green" | "Amber" | "Red" — computed from DueDate, see StatutoryDashboardService.ComputeRag.</summary>
    public string Rag { get; set; } = "Green";
}

public class MarkDeadlineFiledDto
{
    public DateTime? FiledOn { get; set; }
}
