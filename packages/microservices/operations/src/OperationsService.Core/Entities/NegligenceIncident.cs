using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O9 — NEGLIGENCE_INCIDENT (P11): a logged negligence event against an employee. Must be logged within
/// 24h of occurrence (LoggedLate flags a late log) and responded to within 5 days (ResponseDeadline);
/// an overdue response escalates to the MD. A second incident for the same employee within 12 months
/// is a repeat offense → final warning. Distinct from the quality NonConformanceReport.
/// </summary>
public class NegligenceIncident : BaseEntity
{
    public string  IncidentNumber { get; set; } = string.Empty;   // NEG-{year}-{seq}
    public string  EmployeeId     { get; set; } = string.Empty;
    public string  EmployeeName   { get; set; } = string.Empty;
    public string? ProjectId      { get; set; }
    public string? AssignmentId   { get; set; }

    public string Title       { get; set; } = string.Empty;
    public string Description  { get; set; } = string.Empty;
    public NegligenceSeverity Severity { get; set; } = NegligenceSeverity.Minor;

    public DateTime OccurredAt { get; set; }
    public DateTime ReportedAt { get; set; }
    public string   ReportedBy { get; set; } = string.Empty;
    public bool     LoggedLate { get; set; }   // ReportedAt > OccurredAt + 24h

    public NegligenceStatus Status { get; set; } = NegligenceStatus.Logged;
    public DateTime ResponseDeadline { get; set; }   // ReportedAt + 5 days

    public DateTime? EscalatedToMdAt       { get; set; }
    public DateTime? ResponseBreachAlertedAt { get; set; }   // worker once-guard

    public bool IsRepeatOffense   { get; set; }   // 2nd+ within 12 months
    public bool FinalWarningIssued { get; set; }

    public DateTime? ClosedAt { get; set; }

    public ICollection<NegligenceResponse> Responses { get; set; } = new List<NegligenceResponse>();
}
