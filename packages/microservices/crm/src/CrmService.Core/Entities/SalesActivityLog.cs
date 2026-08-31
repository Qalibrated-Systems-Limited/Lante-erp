namespace CrmService.Core.Entities;

/// <summary>P7 — SALES_ACTIVITY_LOG. Daily SE activity rollup (submitted by 5PM): calls, meetings,
/// proposals, visits.</summary>
public class SalesActivityLog : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime LogDate { get; set; } = DateTime.UtcNow.Date;
    public int CallsMade { get; set; }
    public int MeetingsHeld { get; set; }
    public int ProposalsSent { get; set; }
    public int Visits { get; set; }
    public string? Notes { get; set; }
}
